using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ShaderEditor.Windows
{
    /// <summary>
    /// Interaktionslogik für FullScreen.xaml
    /// </summary>
    public partial class FullScreen : Window
    {
        private OpenGlView GLView;


        public FullScreen()
        {
            InitializeComponent();
            GLView = new OpenGlView();
            GLView.SetupQuad();
        }
    }
}
