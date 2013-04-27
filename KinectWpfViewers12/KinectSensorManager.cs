//------------------------------------------------------------------------------
// <copyright file="KinectSensorManager.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.WpfViewers
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using Microsoft.Kinect;
    
    /// <summary>
    /// A data model wrapper for a KinectSensor.
    /// This class reexposes many of the properties and states of the underlying KinectSensor,
    /// and more can be added if neeed.
    /// It also handles Initing and Uniniting the Sensor when needed, as well as keeping the 
    /// Sensor state in sync with the properties here.  
    /// In general, this class will *not* attempt to populate its own state from the KinectSensor 
    /// - all state management should be done though this class.
    /// The exception to this rule is elevation angle, which is most likely to be based on the physical
    /// setup of the sensor in question.  When the sensor starts running, the ElevationAngle property 
    /// will be set to the current value of the sensor's angle.
    /// This class also performs a few bookkeeping tasks, including exposing "KinectAppConflict"
    /// when detected, managing the KinectSensor angle elevation via background async tasks, 
    /// ensuring that only one Skeleton engine is enabled at a time, and it exposed a number of easily
    /// used events for common state changes.
    /// </summary>
    public class KinectSensorManager : Freezable
    {
        public static readonly DependencyProperty KinectSensorProperty =
            DependencyProperty.Register(
                "KinectSensor",
                typeof(KinectSensor),
                typeof(KinectSensorManager),
                new PropertyMetadata(null, KinectSensorOrStatusChanged));

        public static readonly DependencyProperty KinectSensorStatusProperty =
            DependencyProperty.Register(
                "KinectSensorStatus",
                typeof(KinectStatus),
                typeof(KinectSensorManager),
                new PropertyMetadata(KinectStatus.Undefined, KinectSensorOrStatusChanged));

        [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "ReadOnlyDependencyProperty requires private static field to be initialized prior to the public static field")]
        private static readonly DependencyPropertyKey UniqueKinectIdPropertyKey =
            DependencyProperty.RegisterReadOnly(
                "UniqueKinectId",
                typeof(string),
                typeof(KinectSensorManager),
                new PropertyMetadata(null));

        public static readonly DependencyProperty UniqueKinectIdProperty = UniqueKinectIdPropertyKey.DependencyProperty;

        public static readonly DependencyProperty KinectSensorEnabledProperty =
            DependencyProperty.Register(
                "KinectSensorEnabled",
                typeof(bool),
                typeof(KinectSensorManager),
                new PropertyMetadata(true, (sender, args) => ((KinectSensorManager)sender).EnsureKinectSensorRunningState()));

        [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:ElementsMustBeOrderedByAccess", Justification = "ReadOnlyDependencyProperty requires private static field to be initialized prior to the public static field")] 
        private static readonly DependencyPropertyKey KinectSensorAppConflictPropertyKey =
            DependencyProperty.RegisterReadOnly(
                "KinectSensorAppConflict",
                typeof(bool),
                typeof(KinectSensorManager),
                new PropertyMetadata(false, (sender, args) => ((KinectSensorManager)sender).NotifyAppConflict()));

        public static readonly DependencyProperty KinectSensorAppConflictProperty = KinectSensorAppConflictPropertyKey.DependencyProperty;

        public static readonly DependencyProperty ColorStreamEnabledProperty =
            DependencyProperty.Register(
                "ColorStreamEnabled",
                typeof(bool),
                typeof(KinectSensorManager),
                new PropertyMetadata(true, (sender, args) => ((KinectSensorManager)sender).EnsureColorStreamState()));

        public static readonly DependencyProperty ColorFormatProperty =
            DependencyProperty.Register(
                "ColorFormat",
                typeof(ColorImageFormat),
                typeof(KinectSensorManager),
                new PropertyMetadata(
                    ColorImageFormat.RgbResolution640x480Fps30,
                    (sender, args) => ((KinectSensorManager)sender).EnsureColorStreamState()));

        public static readonly DependencyProperty DepthStreamEnabledProperty =
            DependencyProperty.Register(
                "DepthStreamEnabled",
                typeof(bool),
                typeof(KinectSensorManager),
                new PropertyMetadata(true, (sender, args) => ((KinectSensorManager)sender).EnsureDepthStreamState()));

        public static readonly DependencyProperty DepthFormatProperty =
            DependencyProperty.Register(
                "DepthFormat",
                typeof(DepthImageFormat),
                typeof(KinectSensorManager),
                new PropertyMetadata(DepthImageFormat.Resolution640x480Fps30, (sender, args) => ((KinectSensorManager)sender).EnsureDepthStreamState()));

        public static readonly DependencyProperty DepthRangeProperty =
            DependencyProperty.Register(
                "DepthRange",
                typeof(DepthRange),
                typeof(KinectSensorManager),
                new PropertyMetadata(DepthRange.Default, (sender, args) => ((KinectSensorManager)sender).EnsureDepthStreamState()));

        public static readonly DependencyProperty SkeletonStreamEnabledProperty =
            DependencyProperty.Register(
                "SkeletonStreamEnabled",
                typeof(bool),
                typeof(KinectSensorManager),
                new PropertyMetadata(
                    false, 
                    (sender, args) => ((KinectSensorManager)sender).EnsureSkeletonStreamState(),
                    CoerceSkeletonStreamEnabled));

        public static readonly DependencyProperty SkeletonTrackingModeProperty =
            DependencyProperty.Register(
                "SkeletonTrackingMode",
                typeof(SkeletonTrackingMode),
                typeof(KinectSensorManager),
                new PropertyMetadata(
                    SkeletonTrackingMode.Default,
                    (sender, args) => ((KinectSensorManager)sender).EnsureSkeletonStreamState()));

        public static readonly DependencyProperty SkeletonEnableTrackingInNearModeProperty =
            DependencyProperty.Register(
                "SkeletonEnableTrackingInNearMode",
                typeof(bool),
                typeof(KinectSensorManager),
                new PropertyMetadata(
                    true,
                    (sender, args) => ((KinectSensorManager)sender).EnsureSkeletonStreamState()));

        public static readonly DependencyProperty TransformSmoothParametersProperty =
            DependencyProperty.Register(
                "TransformSmoothParameters",
                typeof(TransformSmoothParameters),
                typeof(KinectSensorManager),
                new PropertyMetadata(
                    default(TransformSmoothParameters),
                    (sender, args) => ((KinectSensorManager)sender).EnsureSkeletonStreamState()));

        public static readonly DependencyProperty ElevationAngleProperty =
            DependencyProperty.Register(
                "ElevationAngle",
                typeof(int),
                typeof(KinectSensorManager),
                new PropertyMetadata(0, (sender, args) => ((KinectSensorManager)sender).EnsureElevationAngle(), CoerceElevationAngle));

        private int targetElevationAngle = int.MinValue;
        private bool isElevationTaskOutstanding;        

        public event EventHandler<KinectSensorManagerEventArgs<KinectSensor>> KinectSensorChanged;

        public event EventHandler<KinectSensorManagerEventArgs<KinectStatus>> KinectStatusChanged;

        public event EventHandler<KinectSensorManagerEventArgs<bool>> KinectRunningStateChanged;

        public event EventHandler<KinectSensorManagerEventArgs<bool>> KinectAppConflictChanged;

        public event EventHandler AudioWasResetBySkeletonEngine;
        
        public KinectSensor KinectSensor
        {
            get { return (KinectSensor)this.GetValue(KinectSensorProperty); }
            set { this.SetValue(KinectSensorProperty, value); }
        }

        public KinectStatus KinectSensorStatus
        {
            get { return (KinectStatus)this.GetValue(KinectSensorStatusProperty); }
            set { this.SetValue(KinectSensorStatusProperty, value); }
        }

        public string UniqueKinectId
        {
            get { return (string)this.GetValue(UniqueKinectIdProperty); }
            private set { this.SetValue(UniqueKinectIdPropertyKey, value); }
        }

        public bool KinectSensorEnabled
        {
            get { return (bool)this.GetValue(KinectSensorEnabledProperty); }
            set { this.SetValue(KinectSensorEnabledProperty, value); }
        }
        
        public bool KinectSensorAppConflict
        {
            get { return (bool)this.GetValue(KinectSensorAppConflictProperty); }
            private set { this.SetValue(KinectSensorAppConflictPropertyKey, value); }
        }

        public bool ColorStreamEnabled
        {
            get { return (bool)this.GetValue(KinectSensorManager.ColorStreamEnabledProperty); }
            set { this.SetValue(KinectSensorManager.ColorStreamEnabledProperty, value); }
        }

        public ColorImageFormat ColorFormat
        {
            get { return (ColorImageFormat)this.GetValue(KinectSensorManager.ColorFormatProperty); }
            set { this.SetValue(KinectSensorManager.ColorFormatProperty, value); }
        }

        public bool DepthStreamEnabled
        {
            get { return (bool)this.GetValue(KinectSensorManager.DepthStreamEnabledProperty); }
            set { this.SetValue(KinectSensorManager.DepthStreamEnabledProperty, value); }
        }

        public DepthImageFormat DepthFormat
        {
            get { return (DepthImageFormat)this.GetValue(KinectSensorManager.DepthFormatProperty); }
            set { this.SetValue(KinectSensorManager.DepthFormatProperty, value); }
        }

        public DepthRange DepthRange
        {
            get { return (DepthRange)this.GetValue(KinectSensorManager.DepthRangeProperty); }
            set { this.SetValue(KinectSensorManager.DepthRangeProperty, value); }
        }

        public bool SkeletonStreamEnabled
        {
            get { return (bool)this.GetValue(KinectSensorManager.SkeletonStreamEnabledProperty); }
            set { this.SetValue(KinectSensorManager.SkeletonStreamEnabledProperty, value); }
        }

        public SkeletonTrackingMode SkeletonTrackingMode
        {
            get { return (SkeletonTrackingMode)this.GetValue(KinectSensorManager.SkeletonTrackingModeProperty); }
            set { this.SetValue(KinectSensorManager.SkeletonTrackingModeProperty, value); }
        }

        public bool SkeletonEnableTrackingInNearMode
        {
            get { return (bool)GetValue(SkeletonEnableTrackingInNearModeProperty); }
            set { SetValue(SkeletonEnableTrackingInNearModeProperty, value); }
        }

        public TransformSmoothParameters TransformSmoothParameters
        {
            get { return (TransformSmoothParameters)this.GetValue(TransformSmoothParametersProperty); }
            set { this.SetValue(TransformSmoothParametersProperty, value); }
        }

        public int ElevationAngle
        {
            get { return (int)this.GetValue(KinectSensorManager.ElevationAngleProperty); }
            set { this.SetValue(KinectSensorManager.ElevationAngleProperty, value); }
        }

        protected override Freezable CreateInstanceCore()
        {
            return new KinectSensorManager();
        }

        /// <summary>
        /// Called as part of Freezable.Freeze() to determine whether this class can be frozen.
        /// We can freeze, but only if the KinectSensor is null.
        /// </summary>
        /// <param name="isChecking">True if this is a query for Freezability, false if this is an actual Freeze call.</param>
        /// <returns>True if a Freeze is legal or has occurred, false otherwise.</returns>
        protected override bool FreezeCore(bool isChecking)
        {
            return (null == this.KinectSensor) && base.FreezeCore(isChecking);
        }

        /// <summary>
        /// Callback that occurs when either the KinectSensor or its status has changed
        /// </summary>
        /// <param name="sender">The source KinectSensorManager</param>
        /// <param name="args">The args, which contain the old and new values</param>
        private static void KinectSensorOrStatusChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            var sensorWrapper = sender as KinectSensorManager;

            if (null == sensorWrapper)
            {
                return;
            }

            var oldSensor = sensorWrapper.KinectSensor;
            var sensor = sensorWrapper.KinectSensor;
            var oldStatus = KinectStatus.Undefined;
            var status = null == sensor ? KinectStatus.Undefined : sensor.Status;

            bool sensorChanged = KinectSensorManager.KinectSensorProperty == args.Property;
            bool statusChanged = KinectSensorManager.KinectSensorStatusProperty == args.Property;

            if (sensorChanged)
            {
                oldSensor = (KinectSensor)args.OldValue;
                
                // The elevation task is per-sensor
                sensorWrapper.isElevationTaskOutstanding = false;

                // This can throw if the sensor is going away or gone.
                try
                {
                    sensorWrapper.UniqueKinectId = (null == sensor) ? null : sensor.UniqueKinectId;
                }
                catch (InvalidOperationException)
                {
                }
            }

            if (statusChanged)
            {
                oldStatus = (KinectStatus)args.OldValue;
            }

            // Ensure that the sensor is uninitialized if the sensor has changed or if the status is not Connected
            if (sensorChanged || (statusChanged && (KinectStatus.Connected != status)))
            {
                sensorWrapper.EnsureSensorUninit(oldSensor);
            }

            bool wasRunning = (null != sensor) && sensor.IsRunning;

            sensorWrapper.InitializeKinectServices();

            bool isRunning = (null != sensor) && sensor.IsRunning;

            sensorWrapper.KinectSensorStatus = status;

            if (sensorChanged && (null != sensorWrapper.KinectSensorChanged))
            {
                sensorWrapper.KinectSensorChanged(sensorWrapper, new KinectSensorManagerEventArgs<KinectSensor>(sensorWrapper, oldSensor, sensor));
            }

            if ((status != oldStatus) && (null != sensorWrapper.KinectStatusChanged))
            {
                sensorWrapper.KinectStatusChanged(sensorWrapper, new KinectSensorManagerEventArgs<KinectStatus>(sensorWrapper, oldStatus, status));
            }

            if ((wasRunning != isRunning) && (null != sensorWrapper.KinectRunningStateChanged))
            {
                sensorWrapper.KinectRunningStateChanged(sensorWrapper, new KinectSensorManagerEventArgs<bool>(sensorWrapper, wasRunning, isRunning));
            }
        }

        /// <summary>
        /// Coerce the requested elevation angle to a valid angle
        /// </summary>
        /// <param name="sender">The source KinectSensorManager</param>
        /// <param name="baseValue">The baseValue to coerce</param>
        /// <returns>A valid elevation angle.</returns>
        private static object CoerceElevationAngle(DependencyObject sender, object baseValue)
        {
            var sensorWrapper = sender as KinectSensorManager;

            if ((null == sensorWrapper) || !(baseValue is int))
            {
                return 0;
            }
            
            // Best guess default values for min/max angles
            int minVal = -27;
            int maxVal = 27;

            if (null != sensorWrapper.KinectSensor)
            {
                minVal = sensorWrapper.KinectSensor.MinElevationAngle;
                maxVal = sensorWrapper.KinectSensor.MaxElevationAngle;
            }

            if ((int)baseValue < minVal)
            {
                return minVal;
            }

            if ((int)baseValue > maxVal)
            {
                return maxVal;
            }

            return baseValue;
        }

        /// <summary>
        /// Coercion to prevent multiple sensors from enabling skeletal tracking simultaneously.
        /// </summary>
        /// <param name="sender">The source KinectSensorManager</param>
        /// <param name="baseValue">The baseValue to coerce</param>
        /// <returns>True if the initial value is true and no other sensors are using Skeleton tracking.</returns>
        private static object CoerceSkeletonStreamEnabled(DependencyObject sender, object baseValue)
        {
            var ksm = sender as KinectSensorManager;

            if (!(baseValue is bool) || (null == ksm))
            {
                return false;
            }

            var val = (bool)baseValue;

            // True if the value is true and either:
            //  This KSM is already enabled 
            //      or
            //  There are no running Skeletal Streams
            return val &&
                   (ksm.SkeletonStreamEnabled ||
                    Microsoft.Kinect.KinectSensor.KinectSensors.All(k => (!k.IsRunning || !k.SkeletonStream.IsEnabled)));
        }

        private static void UninitializeKinectServices(KinectSensor sensor)
        {
            if (null == sensor)
            {
                return;
            }

            // Stop streaming
            sensor.Stop();

            if (null != sensor.AudioSource)
            {
                sensor.AudioSource.Stop();
            }

            if (null != sensor.SkeletonStream)
            {
                sensor.SkeletonStream.Disable();
            }

            if (null != sensor.DepthStream)
            {
                sensor.DepthStream.Disable();
            }

            if (null != sensor.ColorStream)
            {
                sensor.ColorStream.Disable();
            }
        }

        private void NotifyAppConflict()
        {
            if (null != this.KinectAppConflictChanged)
            {
                this.KinectAppConflictChanged(this, new KinectSensorManagerEventArgs<bool>(this, !this.KinectSensorAppConflict, this.KinectSensorAppConflict));
            }
        }

        /// <summary>
        /// This method will ensure that the local state and the state of the KinectSensor itself
        /// is initialized.
        /// </summary>
        private void InitializeKinectServices()
        {
            this.EnsureColorStreamState();
            this.EnsureDepthStreamState();
            this.EnsureSkeletonStreamState();
            this.EnsureElevationAngle();
            this.EnsureKinectSensorRunningState();
        }
        
        private void EnsureKinectSensorRunningState()
        {
            var sensor = this.KinectSensor;

            if ((null == sensor) || (KinectStatus.Connected != sensor.Status))
            {
                return;
            }

            if (this.KinectSensorEnabled)
            {
                if (!sensor.IsRunning)
                {
                    // If this call causes an IOException, this means that the sensor is in use by
                    // another application.
                    try
                    {
                        sensor.Start();

                        // We need to retrieve the elevation angle here because the angle may only be retrieved
                        // on a running sensor.
                        this.ElevationAngle = sensor.ElevationAngle;
                        this.KinectSensorAppConflict = false;
                    }
                    catch (IOException)
                    {
                        this.KinectSensorAppConflict = true;
                    }
                    catch (InvalidOperationException)
                    {
                        // The device went away while we were trying to start
                        sensor.Stop();
                        this.KinectSensorAppConflict = false;
                    }
                }
            }
            else
            {
                sensor.Stop();
                this.KinectSensorAppConflict = false;
            }
        }

        private void EnsureColorStreamState()
        {
            var sensor = this.KinectSensor;

            if ((null == sensor) || (KinectStatus.Connected != sensor.Status))
            {
                return;
            }

            if (this.ColorStreamEnabled)
            {
                try
                {
                    sensor.ColorStream.Enable(this.ColorFormat);
                }
                catch (InvalidOperationException)
                {
                    // The device went away while we were trying to start
                    sensor.ColorStream.Disable();
                    return;
                }
            }
            else
            {
                sensor.ColorStream.Disable();
            }
        }

        private void EnsureDepthStreamState()
        {
            var sensor = this.KinectSensor;

            if ((null == sensor) || (KinectStatus.Connected != sensor.Status))
            {
                return;
            }

            if (this.DepthStreamEnabled)
            {
                try
                {
                    sensor.DepthStream.Enable(this.DepthFormat);
                }
                catch (InvalidOperationException)
                {
                    // The device went away while we were trying to start
                    sensor.DepthStream.Disable();
                    return;
                }

                // If this call causes an InvalidOperationException, this means that the device
                // does not support NearMode.
                try
                {
                    sensor.DepthStream.Range = this.DepthRange;
                }
                catch (InvalidOperationException)
                {
                    this.DepthRange = DepthRange.Default;
                }
            }
            else
            {
                sensor.DepthStream.Disable();
            }
        }

        private void EnsureSkeletonStreamState()
        {
            var sensor = this.KinectSensor;

            if ((null == sensor) || (KinectStatus.Connected != sensor.Status))
            {
                return;
            }

            bool skeletonChangeWillCauseAudioReset = sensor.SkeletonStream.IsEnabled != this.SkeletonStreamEnabled;

            if (this.SkeletonStreamEnabled)
            {
                try
                {
                    sensor.SkeletonStream.Enable(this.TransformSmoothParameters);
                    sensor.SkeletonStream.TrackingMode = this.SkeletonTrackingMode;
                    sensor.SkeletonStream.EnableTrackingInNearRange = this.SkeletonEnableTrackingInNearMode;
                }
                catch (InvalidOperationException)
                {
                    // The device went away while we were trying to start
                    sensor.SkeletonStream.Disable();
                    return;
                }
            }
            else
            {
                sensor.SkeletonStream.Disable();
            }

            // Due a bug in Microsoft.Kinect.dll version 1.X, changing the skeleton engine state
            // causes the AudioStream to reset.  We detect that case here and broadcast an event
            // so other components can reset their audio processing logic.
            if (skeletonChangeWillCauseAudioReset && sensor.IsRunning)
            {
                if (null != this.AudioWasResetBySkeletonEngine)
                {
                    this.AudioWasResetBySkeletonEngine(this, new EventArgs());
                }
            }
        }

        private void EnsureElevationAngle()
        {
            var sensor = this.KinectSensor;

            // We cannot set the angle on a sensor if it is not running.
            // We will therefore call EnsureElevationAngle when the requested angle has changed or if the 
            // sensor transitions to the Running state.
            if ((null == sensor) || (KinectStatus.Connected != sensor.Status) || !sensor.IsRunning)
            {
                return;
            }

            this.targetElevationAngle = this.ElevationAngle;

            // If there already a background task, it will notice the new targetElevationAngle
            if (!this.isElevationTaskOutstanding)
            {
                // Otherwise, we need to start a new task
                this.StartElevationTask();
            }
        }

        private void StartElevationTask()
        {
            var sensor = this.KinectSensor;
            int lastSetElevationAngle = int.MinValue;

            if (null != sensor)
            {
                this.isElevationTaskOutstanding = true;

                Task.Factory.StartNew(
                    () =>
                        {
                            int angleToSet = this.targetElevationAngle;

                            // Keep going until we "match", assuming that the sensor is running
                            while ((lastSetElevationAngle != angleToSet) && sensor.IsRunning)
                            {
                                // We must wait at least 1 second, and call no more frequently than 15 times every 20 seconds
                                // So, we wait at least 1350ms afterwards before we set backgroundUpdateInProgress to false.
                                sensor.ElevationAngle = angleToSet;
                                lastSetElevationAngle = angleToSet;
                                Thread.Sleep(1350);

                                angleToSet = this.targetElevationAngle;
                            }
                        }).ContinueWith(
                            results =>
                                {
                                    // This can happen if the Kinect transitions from Running to not running
                                    // after the check above but before setting the ElevationAngle.
                                    if (results.IsFaulted)
                                    {
                                        var exception = results.Exception;

                                        Debug.WriteLine(
                                            "Set Elevation Task failed with exception " +
                                            exception);
                                    }

                                    // We caught up and handled all outstanding angle requests.  
                                    // However, more may come in after we've stopped checking, so
                                    // we post this work item back to the main thread to determine 
                                    // whether we need to start the task up again.
                                    this.Dispatcher.BeginInvoke((Action)(() =>
                                        {
                                            if (this.targetElevationAngle !=
                                                lastSetElevationAngle)
                                            {
                                                this.StartElevationTask();
                                            }
                                            else
                                            {
                                                // If there's nothing to do, we can set this to false.
                                                this.isElevationTaskOutstanding = false;
                                            }
                                        }));
                                });
            }
        }

        /// <summary>
        /// This method will ensure that the local state and the state of the KinectSensor itself
        /// is uninitialized.
        /// The parameter to this method may be null, and it may already be unintialized.
        /// </summary>
        /// <param name="sensor">The sensor to uninit</param>
        private void EnsureSensorUninit(KinectSensor sensor)
        {
            this.KinectSensorAppConflict = false;

            if (sensor == null)
            {
                return;
            }

            UninitializeKinectServices(sensor);
        }
    }
}