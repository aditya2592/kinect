using System;
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
using System.Windows.Shapes;
using System.Windows.Media.Media3D;

namespace WpfApplication1
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>

    public partial class Window1 : Window
    {
        public int s = 1;
        public GeometryModel3D[] points = new GeometryModel3D[640 * 480];
        public Window1()
        {
            InitializeComponent();
        }
        private GeometryModel3D Triangle(
            double x, double y, double s)
        {
            Point3DCollection corners =
                 new Point3DCollection();
            corners.Add(new Point3D(x, y, 0));
            corners.Add(new Point3D(x, y + s, 0));
            corners.Add(new Point3D(x + s, y + s, 0));
            Int32Collection Triangles =
                             new Int32Collection();
            Triangles.Add(0);
            Triangles.Add(1);
            Triangles.Add(2);
            MeshGeometry3D tmesh =
                    new MeshGeometry3D();
            tmesh.Positions = corners;
            tmesh.TriangleIndices = Triangles;
            tmesh.Normals.Add(new Vector3D(0, 0, -1));
            GeometryModel3D msheet =
                new GeometryModel3D();
            msheet.Geometry = tmesh;
            msheet.Material = new DiffuseMaterial(
                   new SolidColorBrush(Colors.Red));
            return msheet;
        }
        public void DrawCloud(int[] distancepixel)
        {
            DirectionalLight DirLight1 =
                new DirectionalLight();
            DirLight1.Color = Colors.White;
            DirLight1.Direction =
                           new Vector3D(1, 1, 1);
            PerspectiveCamera Camera1 =
                 new PerspectiveCamera();
            Camera1.FarPlaneDistance = 8000;
            Camera1.NearPlaneDistance = 100;
            Camera1.FieldOfView = 10;
            Camera1.Position =
                        new Point3D(160, 120, -1000);
            Camera1.LookDirection =
                        new Vector3D(0, 0, 1);
            Camera1.UpDirection =
                        new Vector3D(0, -1, 0);

            
            Model3DGroup modelGroup = new Model3DGroup();

            int i = 0;
            
            for (int y = 0; y < 480; y += s)
            {
                for (int x = 0; x < 640; x += s)
                {
                    points[i] = Triangle(x, y, s);
                    points[i].Transform =
                      new TranslateTransform3D(0, 0, 0);
                    modelGroup.Children.Add(points[i]);
                    i++;
                }
            }
           
            ModelVisual3D modelsVisual = new ModelVisual3D();
            modelsVisual.Content = modelGroup;
            Viewport3D myViewport = new Viewport3D();
            myViewport.IsHitTestVisible = false;
            myViewport.Camera = Camera1;
            myViewport.Children.Add(modelsVisual);
            canvas1.Children.Add(myViewport);
            myViewport.Height = canvas1.Height;
            myViewport.Width = canvas1.Width;
            Canvas.SetTop(myViewport, 0);
            Canvas.SetLeft(myViewport, 0);
            MainWindow mwin = new MainWindow();
            i = 0;
            for (int y = 0; y < 480; y += s)
            {
                for (int x = 0; x < 640; x += s)
                {

                    // if(mwin.depthframe != null)
                    ((TranslateTransform3D)
                        points[i].Transform).OffsetZ = distancepixel[i];
                    i++;


                }
            }
               
            
              
        }

        
    }
}
