using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace WpfApplication1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
         
        }
        public void DrawChart(int[] count)
        {
            chart1.Series[0].Points.Clear();
            for (int i = 1; i < 3500; i++)
            {
                chart1.Series[0].Points.Add(count[i]);
            }
           
            //MainWindow window = new MainWindow();
            //window.DrawObstacle(window.source, window.colorframe, temp[0], temp[2]);
            //window.GetLocalMin(count);
         
        }
        public void DrawChart2(int[] count)
        {
            chart2.Series[0].Points.Clear();
            for (int i = 1; i < (0x1FFF / 8); i++)
            {
                chart2.Series[0].Points.Add(count[i]);
            }
        }
       
    }
}
