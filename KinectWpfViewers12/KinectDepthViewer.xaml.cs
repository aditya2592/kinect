//------------------------------------------------------------------------------
// <copyright file="KinectDepthViewer.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.WpfViewers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for KinectDepthViewer.xaml
    /// </summary>
    public partial class KinectDepthViewer : KinectViewer
    {
        // color divisors for tinting depth pixels
        private static readonly int[] IntensityShiftByPlayerR = { 1, 2, 0, 2, 0, 0, 2, 0 };
        private static readonly int[] IntensityShiftByPlayerG = { 1, 2, 2, 0, 2, 0, 0, 1 };
        private static readonly int[] IntensityShiftByPlayerB = { 1, 0, 2, 2, 0, 2, 0, 2 };

        private const int RedIndex = 2;
        private const int GreenIndex = 1;
        private const int BlueIndex = 0;
        private static readonly int Bgr32BytesPerPixel = (PixelFormats.Bgr32.BitsPerPixel + 7) / 8;

        private DepthImageFormat lastImageFormat;
        private short[] pixelData;

        // We want to control how depth data gets converted into false-color data
        // for more intuitive visualization, so we keep 32-bit color frame buffer versions of
        // these, to be updated whenever we receive and process a 16-bit frame.
        private byte[] depthFrame32;
        private WriteableBitmap outputBitmap;

        // The Dispatcher and KinectSensor for background thread processing
        private ProcessingThread processingThread;

        public KinectDepthViewer()
        {
            InitializeComponent();
        }

        protected override void OnKinectSensorChanged(object sender, KinectSensorManagerEventArgs<KinectSensor> args)
        {
            if (null == args)
            {
                throw new ArgumentNullException("args");
            }

            if (null != this.processingThread)
            {
                // The background thread handles sensor changes and events, so call the processingThread object
                // to switch sensors.
                this.processingThread.SensorChanged(args.OldValue, args.NewValue);
            }
        }
        
        private void DepthImageReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            int imageWidth = 0;
            int imageHeight = 0;
            bool haveNewFormat = false;

            using (DepthImageFrame imageFrame = e.OpenDepthImageFrame())
            {
                if (imageFrame != null)
                {
                    imageWidth = imageFrame.Width;
                    imageHeight = imageFrame.Height;

                    // We need to detect if the format has changed.
                    haveNewFormat = this.lastImageFormat != imageFrame.Format;

                    if (haveNewFormat)
                    {
                        this.pixelData = new short[imageFrame.PixelDataLength];
                        this.depthFrame32 = new byte[imageFrame.Width * imageFrame.Height * Bgr32BytesPerPixel];
                        this.lastImageFormat = imageFrame.Format;

                        // We also need to reallocate the outputBitmap, but WriteableBitmap has 
                        // thread affinity based on the allocating thread.  Since we want this to
                        // be displayed in the UI, we need to do this allocation on the UI thread (below).
                    }

                    imageFrame.CopyPixelDataTo(this.pixelData);
                }
            }

            // Did we get a depth frame?
            if (imageWidth != 0)
            {
                this.ConvertDepthFrame(this.pixelData, ((KinectSensor)sender).DepthStream);

                // The images are converted, update the UI on the UI thread.
                // We use Invoke here instead of BeginInvoke so that the processing frame is blocked from overwriting
                // this.pixelData and this.depthFrame32.
                this.Dispatcher.Invoke((Action)(() =>
                    {
                        if (haveNewFormat)
                        {
                            // A WriteableBitmap is a WPF construct that enables resetting the Bits of the image.
                            // This is more efficient than creating a new Bitmap every frame.
                            this.outputBitmap = new WriteableBitmap(
                                imageWidth,
                                imageHeight,
                                96, // DpiX
                                96, // DpiY
                                PixelFormats.Bgr32,
                                null);

                            this.kinectDepthImage.Source = this.outputBitmap;
                        }

                        this.outputBitmap.WritePixels(
                            new Int32Rect(0, 0, imageWidth, imageHeight),
                            this.depthFrame32,
                            imageWidth * Bgr32BytesPerPixel,
                            0);

                        UpdateFrameRate();
                    }));
            }
        }

        // Converts a 16-bit grayscale depth frame which includes player indexes into a 32-bit frame
        // that displays different players in different colors
        private void ConvertDepthFrame(short[] depthFrame, DepthImageStream depthStream)
        {
            int tooNearDepth = depthStream.TooNearDepth;
            int tooFarDepth = depthStream.TooFarDepth;
            int unknownDepth = depthStream.UnknownDepth;

            // Test that the buffer lengths are appropriately correlated, which allows us to use only one
            // value as the loop condition.
            if ((depthFrame.Length * 4) != this.depthFrame32.Length)
            {
                throw new InvalidOperationException();
            }

            for (int i16 = 0, i32 = 0; i32 < this.depthFrame32.Length; i16++, i32 += 4)
            {
                int player = depthFrame[i16] & DepthImageFrame.PlayerIndexBitmask;
                int realDepth = depthFrame[i16] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                
                if (player == 0 && realDepth == tooNearDepth)
                {
                    // white 
                    this.depthFrame32[i32 + RedIndex] = 255;
                    this.depthFrame32[i32 + GreenIndex] = 255;
                    this.depthFrame32[i32 + BlueIndex] = 255;
                }
                else if (player == 0 && realDepth == tooFarDepth)
                {
                    // dark purple
                    this.depthFrame32[i32 + RedIndex] = 66;
                    this.depthFrame32[i32 + GreenIndex] = 0;
                    this.depthFrame32[i32 + BlueIndex] = 66;
                }
                else if (player == 0 && realDepth == unknownDepth)
                {
                    // dark brown
                    this.depthFrame32[i32 + RedIndex] = 66;
                    this.depthFrame32[i32 + GreenIndex] = 66;
                    this.depthFrame32[i32 + BlueIndex] = 33;
                }
                else
                {
                    // transform 13-bit depth information into an 8-bit intensity appropriate
                    // for display (we disregard information in most significant bit)
                    byte intensity = (byte)(~(realDepth >> 4));

                    // tint the intensity by dividing by per-player values
                    this.depthFrame32[i32 + RedIndex] = (byte)(intensity >> IntensityShiftByPlayerR[player]);
                    this.depthFrame32[i32 + GreenIndex] = (byte)(intensity >> IntensityShiftByPlayerG[player]);
                    this.depthFrame32[i32 + BlueIndex] = (byte)(intensity >> IntensityShiftByPlayerB[player]);
                }
            }
        }

        private void ResetOutput()
        {
            this.Dispatcher.Invoke((Action)(() =>
            {
                if (!this.RetainImageOnSensorChange)
                {
                    this.kinectDepthImage.Source = null;
                    this.outputBitmap = null;
                    this.lastImageFormat = DepthImageFormat.Undefined;
                }
                
                this.ResetFrameRateCounters();
            }));
        }

        private void ShutdownProcessingThread()
        {
            if (null != this.processingThread)
            {
                var temp = this.processingThread;
                this.processingThread = null; 
                temp.BeginInvokeShutdown();
            }

            // We're shut down - no need for this callback at this point.
            this.Dispatcher.ShutdownStarted -= this.Dispatcher_ShutdownStarted;
        }

        private void Dispatcher_ShutdownStarted(object sender, EventArgs e)
        {
            this.ShutdownProcessingThread();
        }

        private void KinectViewer_Loaded(object sender, RoutedEventArgs e)
        {
            this.ShutdownProcessingThread();
            var sensor = this.KinectSensorManager == null ? null : this.KinectSensorManager.KinectSensor;
            this.processingThread = new ProcessingThread(sensor, this.DepthImageReady, this.ResetOutput);

            // We need to shut down the processing thread when this main thread is shut down.
            this.Dispatcher.ShutdownStarted += this.Dispatcher_ShutdownStarted;
        }

        private void KinectViewer_Unloaded(object sender, RoutedEventArgs e)
        {
            this.ShutdownProcessingThread();
        }

        /// <summary>
        /// Helper class to receive frames and process them on a background thread.
        /// </summary>
        private class ProcessingThread
        {
            private readonly EventHandler<DepthImageFrameReadyEventArgs> depthImageReady;
            private readonly Action resetOutput;

            private KinectSensor kinectSensor;
            private Dispatcher dispatcher;

            /// <summary>
            /// Initializes a new instance of the ProcessingThread class, which will call the provided delegates when frames 
            /// are ready or when the sensor has changed.
            /// </summary>
            /// <param name="kinectSensor">Optional initial value for the target KinectSensor.</param>
            /// <param name="depthImageReady">Delegate to invoke when frames are ready.  Will be invoked on background thread.</param>
            /// <param name="resetOutput">Delegate to invoke when the sensor is reset.  Will be invoked on background thread.</param>
            public ProcessingThread(
                KinectSensor kinectSensor,
                EventHandler<DepthImageFrameReadyEventArgs> depthImageReady,
                Action resetOutput)
            {
                if (null == depthImageReady)
                {
                    throw new ArgumentNullException("depthImageReady");
                }

                if (null == resetOutput)
                {
                    throw new ArgumentNullException("resetOutput");
                }

                this.depthImageReady = depthImageReady;
                this.resetOutput = resetOutput;

                // Use this event to know when the processing thread has started.
                var startEvent = new ManualResetEventSlim();
                var processingThread = new Thread(this.ProcessDepthThread);
                processingThread.Name = "KinectDepthViewer-ProcessingThread";

                // Start up the processing thread to do the depth conversion off of the main UI thread.
                processingThread.Start(startEvent);

                // Wait for the thread to start.
                startEvent.Wait();

                if (null == this.dispatcher)
                {
                    throw new InvalidOperationException("StartEvent was signaled, but no Dispatcher was found.");
                }

                this.SensorChanged(null, kinectSensor);
            }

            /// <summary>
            /// Method to invoke to change target KinectSensors.  May be called from any thread, though it is not thread safe.
            /// </summary>
            /// <param name="oldSensor">The old KinectSensor.</param>
            /// <param name="newSensor">The new KinectSensor.</param>
            public void SensorChanged(KinectSensor oldSensor, KinectSensor newSensor)
            {
                if (null == this.dispatcher)
                {
                    throw new InvalidOperationException();
                }

                // Make sure to invoke this async on the processing thread.
                this.dispatcher.BeginInvoke((Action)(() =>
                {
                    if (null != oldSensor)
                    {
                        this.kinectSensor.DepthFrameReady -= this.depthImageReady;
                        this.kinectSensor = null;
                    }

                    if (null != newSensor)
                    {
                        this.kinectSensor = newSensor;
                        this.kinectSensor.DepthFrameReady += this.depthImageReady;
                    }

                    this.resetOutput();
                }));
            }

            /// <summary>
            /// Begins the shutdown of the background thread.  Returns immediately, shutdown will be async.
            /// </summary>
            public void BeginInvokeShutdown()
            {
                this.dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            }

            // The thread process for processing
            private void ProcessDepthThread(object startEventObj)
            {
                var startEvent = (ManualResetEventSlim)startEventObj;

                try
                {
                    var dispatcher = Dispatcher.CurrentDispatcher;

                    // Post a work item to complete the handshake with the main thread so that we can ensure
                    // that everything is running.
                    dispatcher.BeginInvoke((Action)(() =>
                    {
                        this.dispatcher = dispatcher;
                        startEvent.Set();
                    }));

                    // Unsubscribe if we're being shut down.
                    dispatcher.ShutdownStarted += (sender, args) =>
                    {
                        if (null != this.kinectSensor)
                        {
                            this.kinectSensor.DepthFrameReady -= depthImageReady;
                            this.kinectSensor = null;
                        }
                    };

                    Dispatcher.Run();
                }
                finally
                {
                    // Even if something goes wrong, we should ensure that the event is set to 
                    // unblock the main thread.
                    startEvent.Set();
                }
            }
        }
    }
}
