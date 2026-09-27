using System;using System.Windows.Forms;using System.Drawing;
class T{ [STAThread] static void Main(){ var f=new Form(); f.Text="ok"; Console.WriteLine(typeof(Form).Assembly.Location); } }
