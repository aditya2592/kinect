//------------------------------------------------------------------------------
// <copyright file="KinectSettings.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.WpfViewers
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for KinectSettings.xaml
    /// </summary>
    public partial class KinectSettings : KinectControl
    {
        private readonly KinectSettingsViewModel viewModel = new KinectSettingsViewModel();

        public KinectSettings()
        {
            // We bind the ViewModel's KinectSensorManager to this class's property so changes
            // will be propagated.
            var kinectSensorBinding = new Binding("KinectSensorManager");
            kinectSensorBinding.Source = this;
            BindingOperations.SetBinding(this.viewModel, KinectSettingsViewModel.KinectSensorManagerProperty, kinectSensorBinding);

            this.InitializeComponent();

            this.ViewModelRoot.DataContext = this.viewModel;
        }

        private void Slider_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var fe = sender as FrameworkElement;

            if (null != fe)
            {
                if (fe.CaptureMouse())
                {
                    e.Handled = true;
                }
            }
        }

        private void Slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            var fe = sender as FrameworkElement;

            if (null != fe)
            {
                if (fe.IsMouseCaptured)
                {
                    fe.ReleaseMouseCapture();
                    e.Handled = true;
                }
            }
        }

        private void Slider_MouseMove(object sender, MouseEventArgs e)
        {
            var fe = sender as FrameworkElement;

            if (null != fe)
            {
                if (fe.IsMouseCaptured && (null != this.viewModel.KinectSensorManager) && (null != this.viewModel.KinectSensorManager.KinectSensor))
                {
                    var position = Mouse.GetPosition(this.SliderTrack);
                    int newAngle = -27 + (int)Math.Round(54.0 * (this.SliderTrack.ActualHeight - position.Y) / this.SliderTrack.ActualHeight);
                    
                    if (newAngle < -27)
                    {
                        newAngle = -27;
                    }
                    else if (newAngle > 27)
                    {
                        newAngle = 27;
                    }

                    this.viewModel.KinectSensorManager.ElevationAngle = newAngle;
                }
            }
        }
    }
}
