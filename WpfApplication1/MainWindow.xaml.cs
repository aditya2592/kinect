using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Kinect;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using Microsoft.Office.Interop.PowerPoint;
using System.Threading;
using System.IO.Ports;
 

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        KinectSensor newsensor;
        int[] count; //array containing number of pixels at given depth
        public ColorImageFrame colorframe; //access current colorframe
        public BitmapSource source; //access current colorimage source
        public short[] depthpixels; //raw depth data for each pixel
        byte[] pixeldata; //containing color pixels
        public DepthImageFrame depthframe;
        string mode, resolution;
        Form1 graph;
        Window1 window1;
        int[] distancepixel = new int[640 * 480];
        int temp = 50;
        int fire = 0;
        Microsoft.Office.Interop.PowerPoint.Application app = new Microsoft.Office.Interop.PowerPoint.Application();
        //public System.IO.Ports.SerialPort serial;
        public System.IO.Ports.SerialPort serial = new SerialPort();


        public MainWindow()
        {
            InitializeComponent();
            
           
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooser1.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);

            
            serial.PortName = "COM7";
            serial.BaudRate = 9600;
            serial.Open();
        
        }

        void kinectSensorChooser1_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            KinectSensor oldsensor = (KinectSensor)e.OldValue;
            
            if(oldsensor!=null)
                oldsensor.Stop();

            newsensor = (KinectSensor)e.NewValue;
            if (newsensor.Status == KinectStatus.Connected)
            {
                newsensor.ColorStream.Enable();
                DialogResult result = System.Windows.Forms.MessageBox.Show("Yes for 320x240. No for 640x480", "Choose Sensor Resolution", MessageBoxButtons.YesNo);
                if(result == System.Windows.Forms.DialogResult.Yes)
                {
                    newsensor.DepthStream.Enable(DepthImageFormat.Resolution320x240Fps30);
                }
                else
                {
                    newsensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                }
                newsensor.SkeletonStream.Enable();
                newsensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(newsensor_AllFramesReady);
                try
                {
                    newsensor.Start();
                }
                catch (System.IO.IOException) {
                    kinectSensorChooser1.AppConflictOccurred();
                }
            }
           
            
        }
        public int GetPixelDepth(int x, int y, short[] depthdata, int width)
        {

            int d = (ushort)depthdata[x + y * width];
            d = d >> 3;
            return d;
        
        }
        
        public void DrawObstacle(BitmapSource source, ColorImageFrame colorframe, double low=0, double high = 0)
        {
            int i = 0,j=0, temp=0,mindistance = 5000;
            double minangle = 0;
           
            DrawingGroup aDrawingGroup = new DrawingGroup();
            DrawingGroup bDrawingGroup = new DrawingGroup();
            System.Windows.Point spiralcenter =  new System.Windows.Point(0, 0);

            ImageDrawing obstacleimage =
                new ImageDrawing(
                    source,
                    new Rect(0, 0, depthframe.Width, depthframe.Height));
            aDrawingGroup.Children.Add(obstacleimage);
            GeometryDrawing obstacle = new GeometryDrawing();
            GeometryDrawing obstaclemin = new GeometryDrawing();

            System.Windows.Point center = new System.Windows.Point(0, 0);


           


            if (mode == "Closest" || mode == "Continuos")
            {
               
                    for (i = 0; i < depthframe.Width; i++)
                    {
                        for (j = 0; j < depthframe.Height; j++)
                        {
                                
                                temp = GetPixelDepth(i, j, depthpixels, depthframe.Width);
                                int player = depthpixels[i+j*depthframe.Width] & DepthImageFrame.PlayerIndexBitmask; //Detect humans only
                                if (temp < mindistance && temp > 0 && player > 0) //Player condition seperates humans
                                {
                                    mindistance = temp;
                                    center = new System.Windows.Point(i, j);
                                    obstaclemin =
                                         new GeometryDrawing(
                                           new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 189, 23, 20)),
                                           new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1),
                                           new EllipseGeometry(center, 10, 10)
                                    );  //Draw min dist point
                                    
                                }
                           
                                    
                               

                        }
                    }
                minangle = ((center.X - 160) / 320) * 53;
                if (center.X < colorframe.Width && center.Y < colorframe.Width && center.X > 0 && center.Y > 0)
                {
                    textBox3.Text = "X of center : " + Convert.ToString(center.X) + "\nY of center : " + Convert.ToString(center.Y) + "\nMin Distance: " + Convert.ToString(mindistance) + "\nMin Angle: " + Convert.ToString(minangle);

                }
                else
                {
                    textBox3.Text = "Not found in Image";
                    mindistance = -1;
                }
              //  using ( StreamWriter writer = new StreamWriter("D:/kinectAA21.06.2012/kdata.txt"))
               //     RunBot(mindistance, ((center.X - 320) / 640) * 57,writer);
            
                if (mindistance > 1000)
                { 
                    if (minangle > 10)
                    {
                        serial.Write("a");
                        textBox10.Text = "Left";
                    }
                    else if (minangle < -10)
                    {
                        serial.Write("d");
                        textBox10.Text = "Right";
                    }
                    else
                    {
                        serial.Write("w");
                        textBox10.Text = "Forward";
                    }
                }
                else if (mindistance <= 1000 && mindistance>0)
                {
                    if (minangle > 10)
                    {
                        serial.Write("a");
                        textBox10.Text = "Left";
                    }
                    else if (minangle < -10)
                    {
                        serial.Write("d");
                        textBox10.Text = "Right";

                    }
                    else
                    {
                        serial.Write("w");
                        textBox10.Text = "Forward";
                    }

                }
                else
                {
                    serial.Write("o");
                    textBox10.Text = "Stop";
                }

               

            }
            else if (mode == "Spiral")
            {
                int minvertical = 5000;
                for (i = 0; i < colorframe.Width; i++)
                {
                    for (j = 0; j < colorframe.Height; j++)
                    {
                        temp = GetPixelDepth(i, j, depthpixels, depthframe.Width);
                        if (temp < minvertical && temp > 0)
                        {
                            minvertical = temp;
                         
                           
                        }
                    }
                    GeometryDrawing lines =
                                         new GeometryDrawing(
                                           new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 189, 23, 20)),
                                           new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1),
                                           new LineGeometry(spiralcenter, new System.Windows.Point(i - 320, -minvertical))
                                         );
                    Console.WriteLine(new System.Windows.Point(i - 320, -minvertical));
                    bDrawingGroup.Children.Add(lines);
                }

                DrawingImage Image2 = new DrawingImage(bDrawingGroup);
                image3.Source = Image2;
               
            }
            else if (mode == "Limits")
            {
                using (StreamWriter writer = new StreamWriter("D:/kinectAA21.06.2012/kdata.txt"))
                    aDrawingGroup = GetMinDistance(Convert.ToDouble(textBox7.Text), Convert.ToDouble(textBox8.Text), aDrawingGroup, writer);
                
            }
            else if (mode == "LimitsAuto")
            {
                using (StreamWriter writer = new StreamWriter("D:/kinectAA21.06.2012/kdata.txt"))
                {
                    aDrawingGroup = GetMinDistance(-10, 10, aDrawingGroup, writer);
                    aDrawingGroup = GetMinDistance(-26, -10, aDrawingGroup, writer);
                    aDrawingGroup = GetMinDistance(10, 26, aDrawingGroup, writer);
                }
            }

            else if (mode == "Range" || mode == "AllObstacles")
            {
                if (mode == "Range")
                {
                    low = slider1.Value;
                    high = slider2.Value;
                }
                for (i = 0; i < colorframe.Width; i++)
                {
                    for (j = 0; j < colorframe.Height; j++)
                    {
                        temp = GetPixelDepth(i, j, depthpixels, depthframe.Width);
                        if (temp > low && temp < high)
                        {
                            center = new System.Windows.Point(i, j);
                            obstacle =
                                 new GeometryDrawing(
                                     new SolidColorBrush(System.Windows.Media.Color.FromArgb(102, 181, 243, 20)),
                                     new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1),
                                     new EllipseGeometry(center, 10, 10)
                                 );
                            aDrawingGroup.Children.Add(obstacle);
                            break;
                        }

                    }


                }


            }
            if (mode != "Spiral")
            {
                aDrawingGroup.Children.Add(obstacle);
                aDrawingGroup.Children.Add(obstaclemin);


                DrawingImage Image = new DrawingImage(aDrawingGroup);
                image2.Source = Image;
            }
           

        }
        public DrawingGroup GetMinDistance(double anglemin, double anglemax, DrawingGroup aDrawingGroup, StreamWriter writer)
        {
            int i = 0, j = 0, temp = 0, mindistance = 5000;
            System.Windows.Point center = new System.Windows.Point();
           
          
            GeometryDrawing obstaclemin = new GeometryDrawing();

            int upper = (int)(((anglemax / 57) * depthframe.Width) + depthframe.Width/2);
            int lower = (int)(((anglemin / 57) * depthframe.Width) + depthframe.Width/2);
           

            GeometryDrawing left =
                            new GeometryDrawing(
                              new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 189, 23, 20)),
                              new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1),
                              new LineGeometry(new System.Windows.Point(lower, 0), new System.Windows.Point(lower, colorframe.Height))
                       );
            GeometryDrawing right =
                          new GeometryDrawing(
                            new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 189, 23, 20)),
                            new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1),
                            new LineGeometry(new System.Windows.Point(upper, 0), new System.Windows.Point(upper, colorframe.Height))
                     );
            aDrawingGroup.Children.Add(left);
            aDrawingGroup.Children.Add(right);

            for (i = lower; i < upper; i++)
            {
                for (j = 0; j < depthframe.Height; j++)
                {
                    temp = GetPixelDepth(i, j, depthpixels, depthframe.Width);
                    if (temp < mindistance && temp > 0)
                    {
                        mindistance = temp;
                        center = new System.Windows.Point(i, j);
                        obstaclemin =
                             new GeometryDrawing(
                               new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 189, 23, 20)),
                               new System.Windows.Media.Pen(System.Windows.Media.Brushes.Black, 1),
                               new EllipseGeometry(center, 10, 10)
                        );                      
                    }
                }
            }

            if (center.X < colorframe.Width && center.Y < colorframe.Width && center.X > 0 && center.Y > 0)
            {
               textBox3.AppendText("Searching between " + anglemin + " and " + anglemax + "\nX of center : " + Convert.ToString(center.X) + "\nY of center : " + Convert.ToString(center.Y) + "\nMin Distance: " + Convert.ToString(mindistance) + "\nMin Angle: " + Convert.ToString(((center.X - 320) / 640) * 57) + "\n");
            }
            else
                textBox3.AppendText("Not found in Image");
            RunBot(mindistance, ((center.X - 320) / 640) * 57, writer); 
            aDrawingGroup.Children.Add(obstaclemin);
            return aDrawingGroup; 
                
        }
        public void GetLocalMin(int[] count)
        {
            int[] temp = new int[50];
            int j = 0;
            for (int i = 1; i < 3500; i++)
            {
                if (count[i] < count[i + 10] && count[i] < count[i - 10] && count[i] != 0)
                {
                    temp[j] = i;
                    j++;

                }
            }
            DrawObstacle(source, colorframe, temp[0], temp[2]);
        }
        public void RunBot(int mindist, double angle, StreamWriter writer) 
        {
            
            
                writer.WriteLine(Convert.ToString(mindist));
                writer.WriteLine(Convert.ToString((int)angle));
               
            
        }
        void newsensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
           
            using (colorframe = e.OpenColorImageFrame())
            {
                if (colorframe == null)
                {
                    return;
                }


                pixeldata = new byte[colorframe.PixelDataLength];
                colorframe.CopyPixelDataTo(pixeldata);
                source = BitmapSource.Create(colorframe.Width, colorframe.Height, 96.0, 96.0, PixelFormats.Bgr32, null, pixeldata, colorframe.Width * 4);
                
                image1.Source = source;
             

            }
           
            using (depthframe = e.OpenDepthImageFrame())
            {
                if (depthframe == null)
                {
                    return;
                }
                
                int temp = 0, x = 0, y = 0;
                double angleX = 0 , angleY = 0 ;
                string start;
                
                count = new int[4000 * sizeof(double)];
                depthpixels = new short[depthframe.PixelDataLength];
                depthframe.CopyPixelDataTo(depthpixels);
                
                using (StreamReader reader = new StreamReader("kinectstart.txt"))
                {
                   start = reader.ReadLine();
                }
               if(start == "1")
               {
                   using (StreamWriter writer = new StreamWriter("image3Dkinect.txt"))
                   {
                       for (x = 0; x < depthframe.Width; x++)
                       {
                           for (y = 0; y < depthframe.Height; y++)
                           {
                                 temp = GetPixelDepth(x, y, depthpixels, depthframe.Width);
                                 angleX = (x - depthframe.Width/2);
                                 angleX = (angleX / depthframe.Width) * 57;
                                 angleY = (y - depthframe.Height/2);
                                 angleY = (angleY / depthframe.Height) * 43;

                                 writer.WriteLine((int)(temp * Math.Tan(angleX * Math.PI / 180)) + " " + (int)(temp * Math.Tan(angleY * Math.PI / 180)) + " " + temp.ToString());
                           }
                       }
                   }
                   using (StreamWriter writer = new StreamWriter("kinectstart.txt"))
                   {
                       writer.Write(0);
                   }

               }
                if (mode == "Continuos" || mode == "LimitsAuto")
                {
                    
                    DrawObstacle(source, colorframe); //Draw closest obstacle in continuos frames
                }
                
                else
                {
                    float distance = 5000;
                    int player = 0, playerdist = 0;
                    for (int i = 0; i < depthpixels.Length; i++)
                    {
                        player = depthpixels[i] & DepthImageFrame.PlayerIndexBitmask;
                        if (player > 0)
                        {
                            playerdist = depthpixels[i] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                        }
                        //gets the depth value
                        int depth = depthpixels[i] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                        if (depth > 0 && depth < distance)
                        {
                            distance = depth;
                        }
                        temp = ((ushort)depthpixels[i]) >> 3;
                        distancepixel[i] = temp;
                        count[temp]++; //increments the counter corresponding to the distance of the current pixel
                    }


                    //float mindist = depthpixels.Min(element => Math.Abs(element));
                    //textBox1.Text = Convert.ToString(GetPixelDepth(depthframe.Width/2, depthframe.Height/2, depthpixels, depthframe.Width));
                    textBox2.Text = Convert.ToString(playerdist);
                    
                }
                
               
            }
            using (SkeletonFrame SFrame = e.OpenSkeletonFrame())
            {
               /* if (SFrame == null)
                {
                    return;
                }
                Skeleton[] Skeletons = new Skeleton[SFrame.SkeletonArrayLength];
                SFrame.CopySkeletonDataTo(Skeletons);
                foreach (Skeleton S in Skeletons)
                {
                    if (S.TrackingState == SkeletonTrackingState.Tracked)
                    {
                        SkeletonPoint joint = S.Joints[JointType.HandLeft].Position;

                        ColorImagePoint Cloc = newsensor.MapSkeletonPointToColor(joint, ColorImageFormat.RgbResolution640x480Fps30);
                        
                        textBox9.Text = Cloc.X.ToString();


                        if (Cloc.X > 150 && Cloc.X < 350)
                        {
                           // System.Windows.MessageBox.Show("Fired");
                            
                            
                            try
                            {
                                SlideShowWindow window = app.ActiveWindow.Presentation.SlideShowWindow;
                                 if (Cloc.X > 200)
                                     window.View.Next();
                                 else if(Cloc.X < 200)
                                     window.View.Previous();

                                

                            }
                            catch (System.Runtime.InteropServices.COMException)
                            {


                            }
                        }
                       
                        //temp = Cloc.X;
                        
                    }
                }
                /*try
                {
                    SlideShowWindow window = app.ActiveWindow.Presentation.SlideShowWindow;

                }
                catch (System.Runtime.InteropServices.COMException)
                {


                }
                
                */
                //Slides slides = app.ActiveWindow.Presentation.Slides;
                //System.Windows.MessageBox.Show(slides.Count.ToString());
                
            }
           
           
               
        }
       
        void LocateObstacle()
        {
            int[] index = new int[4000 * sizeof(double)];
            int[] nonzero = new int[4000 * sizeof(double)];
            int[] depth = new int[4000 * sizeof(double)];
            int i = 0,temp=0;
            if (mode != "Spiral")
            {
                graph = new Form1();
                graph.Show();
                for (i = 11; i < (0x1FFF / 4); i++)
                {
                    if (count[i] != 0)
                    {
                        nonzero[temp] = count[i];
                        depth[temp] = i;
                        temp++;
                    }

                }
                graph.DrawChart(count); //use count to plot original
                graph.DrawChart2(nonzero);
            }
            //textBox3.Text = Convert.ToString(Array.BinarySearch(count, count.Max()));
            //textBox3.Text = Convert.ToString(Array.Find(count, EqualToMax));
            if(mode == "AllObstacles")
                GetLocalMin(count);
            else if (mode == "Cloud")
            {
                window1 = new Window1();
                window1.Show();
                window1.DrawCloud(distancepixel);
                
                
            }

            else
                DrawObstacle(source, colorframe);
           
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            kinectSensorChooser1.Kinect.Stop();
            
        }

        private void button1_Click(object sender, RoutedEventArgs e)
        {
          

            LocateObstacle();
            
            
        }

        private void image1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            
            System.Windows.Point position = e.GetPosition(this);
            textBox1.Text = Convert.ToString(GetPixelDepth((int)position.X * 2, (int)position.Y * 2, depthpixels, depthframe.Width));
            textBox4.Text = Convert.ToString(position);

        }

        private void image2_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

            System.Windows.Point position = e.GetPosition(this);
            textBox1.Text = Convert.ToString(GetPixelDepth((int)position.X , (int)position.Y , depthpixels, depthframe.Width));
            textBox4.Text = Convert.ToString(position);
        }

        private void listBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBoxItem lbi = ((sender as System.Windows.Controls.ListBox).SelectedItem as ListBoxItem);
            mode = lbi.Content.ToString();
        }

        private void buttonstop_Click(object sender, RoutedEventArgs e)
        {
            serial.Write("o");
            textBox10.Text = "Stop";
            window1.Close();
        }

       


        
       
     


       

        

       

       
       
    }
}
