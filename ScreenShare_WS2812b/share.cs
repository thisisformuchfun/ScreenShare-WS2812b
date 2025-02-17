﻿using System;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenShare_WS2812b
{
    public partial class Share : Form
    {
        public Share()
        {
            InitializeComponent();
        }



        //Variables
        int iLEDwidth = 0;
        int iLEDheight = 0;
        int iPort = 0;
        int iOpenTasks = 0;
        int iDropedFrames = 0;
        string sIP = "";
        bool bConnected = false;
        public bool bControlls = false;
        byte[] bySendRGB565;

        //Create an Array, Every LED-Pixel gets a Colorspace
        Color[,] ledPixCol;



        private void Share_Load(object sender, EventArgs e)
        {
            //Make the Window transparent so the Screenshot can be taken
            this.TransparencyKey = Color.LimeGreen;

            //If there is no saved Config open the Configuration Editor
            if (!Convert.ToBoolean(ConfigurationManager.AppSettings["config"]))
            {
                this.Hide();
                using (var form = new configEditor())
                {
                    var result = form.ShowDialog();
                    while (result == DialogResult.Cancel)
                    {
                        //If the User tries to start the Program without saving a Config an error Message is displayed
                        var retry = MessageBox.Show("You can´t use this Program without a Configuration.\nIf you press Cancel the Program will be closed.", "Please create a Configuration", MessageBoxButtons.RetryCancel, MessageBoxIcon.Error);
                        if (retry == DialogResult.Retry)
                        {
                            result = form.ShowDialog();
                        }
                        else
                        {
                            this.Close();
                        }
                    }
                }
            }
            readConfig();
        }



        private void btnConfig_Click(object sender, EventArgs e)
        {
            //Open the Configuration Editor
            using (var form = new configEditor())
            {
                var result = form.ShowDialog();
                if (result == DialogResult.OK)
                {
                    //If the Configuration is Changed reload all the variables
                    readConfig();
                    //If the Configuration has changed the Connection to the ESP has to be reetablished so the Changes can take effect.
                    btnConnect.PerformClick();
                }
            }
        }



        //Every time the Configuration is changed the Variables has to be updated
        public void readConfig()
        {
            //Try to set the Variables to the Saved Config
            ConfigurationManager.RefreshSection("appSettings");
            try
            {
                sIP = ConfigurationManager.AppSettings["ip-adress"];
                iPort = Convert.ToInt16(ConfigurationManager.AppSettings["port"]);
                timer1.Interval = Convert.ToInt32(ConfigurationManager.AppSettings["refresh"]);
                this.TopMost = Convert.ToBoolean(ConfigurationManager.AppSettings["top"]);
            }
            catch (Exception)
            {
                MessageBox.Show("There is a problem with the Configuration.\tPlease set a new Configuration and try again", "Config Problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }



        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                //Connect to the ESP and send the max Brightness
                TcpClient client = new TcpClient(sIP, iPort);
                NetworkStream nwStream = client.GetStream();
                byte[] bySend = new byte[2];
                bySend[0] = (byte)'C';
                bySend[1] = Convert.ToByte(ConfigurationManager.AppSettings["brightness"]);
                nwStream.Write(bySend, 0, bySend.Length);

                //Recive the Matrix Configuration from the ESP
                byte[] bytesToRead = new byte[client.ReceiveBufferSize];
                int bytesRead = nwStream.Read(bytesToRead, 0, client.ReceiveBufferSize);
                string returnData = Encoding.ASCII.GetString(bytesToRead, 0, bytesRead);
                string[] buffer = returnData.Split(';');
                iLEDwidth = Convert.ToInt32(buffer[0]);
                iLEDheight = Convert.ToInt32(buffer[1]);

                client.Close();

                //Create an Array of Pixels with the Mesurements from the Matrix
                ledPixCol = new Color[iLEDwidth, iLEDheight];
                bySendRGB565 = new byte[(iLEDheight * iLEDwidth) * 2 + 1];

                //If the Connection was successfull show to the User
                bConnected = true;
                btnConnect.BackColor = Color.Green;
                btnConnect.FlatAppearance.MouseOverBackColor = Color.LightGreen;
                btnConnect.Text = "Connected";

                //The Matrix Size is displayed to verify the correct configuration
                updateInfo();
            }
            catch (Exception)
            {
                MessageBox.Show("The answer has taken to long.\nPlease verify the configured IP-Address and port.\nTry again after changing the Config", "Time out", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void btnStart_Click(object sender, EventArgs e)
        {
            timer1.Enabled = true;
            iDropedFrames = 0;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
        }



        private async void timer1_Tick(object sender, EventArgs e)
        {
            //If the ESP isnt connected the Capture doesnt start
            //The Connection is important to know the Size of the Matrix so the Program can properly resize the Captured image
            if (!bConnected)
            {
                timer1.Enabled = false;
                MessageBox.Show("The ESP is not Connected,\nPlease establish a connection before you start to share the Screen.", "No Connection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }


            //take a Screenshot of all monitors and crop it to the Size of the Transparent panel
            Graphics myGraphics = CreateGraphics();
            Size s = new Size(pnlShot.Size.Width - 3, pnlShot.Size.Height - 3);
            var cropScreen = new Bitmap(s.Width, s.Height, myGraphics);
            Graphics memoryGraphics = Graphics.FromImage(cropScreen);
            memoryGraphics.CopyFromScreen((Location.X + 21), (Location.Y + 61), 0, 0, s);
            pbPrevOrig.BackgroundImage = cropScreen;


            //Set Variables to resize the Image
            int originalWidth = pbPrevOrig.BackgroundImage.Width;
            int originalHeight = pbPrevOrig.BackgroundImage.Height;


            //Display the Cropped Screenshot in the Preview-window
            var thumbnail = new Bitmap(iLEDwidth, iLEDheight);
            var graphic = Graphics.FromImage(thumbnail);

            //Calculate the ratio so the resized Image wont be stretched.
            double ratioX = (double)iLEDwidth / (double)originalWidth;
            double ratioY = (double)iLEDheight / (double)originalHeight;
            double ratio = ratioX < ratioY ? ratioX : ratioY;
            int newHeight = Convert.ToInt32(originalHeight * ratio);
            int newWidth = Convert.ToInt32(originalWidth * ratio);
            int posX = Convert.ToInt32((iLEDwidth - (originalWidth * ratio)) / 2);
            int posY = Convert.ToInt32((iLEDheight - (originalHeight * ratio)) / 2);

            //If the Image dosnt fit on the Matrix make a black border
            graphic.Clear(Color.Black);
            graphic.DrawImage(cropScreen, posX, posY, newWidth, newHeight);


            //Draw an Pixelated Matrix preview in the Preview-window
            //If you just put the low res Image in the Picturebox it will try to upscale it and smooth out the edges.
            Bitmap pixPrev = new Bitmap(pbPrevMatrix.Size.Width, pbPrevMatrix.Size.Height);
            using (Graphics gr = Graphics.FromImage(pixPrev))
            {
                for (int x = 0; x < iLEDwidth; x++)
                {
                    for (int y = 0; y < iLEDheight; y++)
                    {
                        //Create a Previewimage from the Colors of the Pixels, Every LED will be displayed as a Square in the Preview-window
                        Rectangle rect = new Rectangle((pbPrevMatrix.Size.Width / iLEDwidth) * x, (pbPrevMatrix.Size.Height / iLEDheight) * y, (pbPrevMatrix.Size.Width / iLEDwidth), (pbPrevMatrix.Size.Height / iLEDheight));
                        Brush brCol = new SolidBrush(thumbnail.GetPixel(x, y));
                        gr.FillRectangle(brCol, rect);

                        //Save The Color to the Array so it can be send to the ESP later
                        ledPixCol[x, y] = thumbnail.GetPixel(x, y);
                    }
                }
                pbPrevMatrix.Image = pixPrev;
            }


            int iIndex = 0;

            //Use the First byte to signal the ESP what kind of Packe is send.
            bySendRGB565[iIndex++] = (byte)'P';

            for (int y = 0; y < iLEDheight; y++)
            {
                for (int x = 0; x < iLEDwidth; x++)
                {
                    //The colors are convertet to RGB565 (16 Bit) so the ESP dosnt have to do the Conversion
                    UInt16 uiRGB565 = 0;
                    uiRGB565 = Convert.ToUInt16(ledPixCol[x, y].B >> 3);
                    uiRGB565 |= Convert.ToUInt16((ledPixCol[x, y].G >> 2) << 5);
                    uiRGB565 |= Convert.ToUInt16((ledPixCol[x, y].R >> 3) << 11);

                    //The 16 Bit has to be split into two different byte so it can be transmitted via TCP
                    bySendRGB565[iIndex++] = (byte)((uiRGB565 & 0xFF00) >> 8);
                    bySendRGB565[iIndex++] = (byte)(uiRGB565 & 0x00FF);
                }
            }

            //Creatre a variable to cancle the async tast
            CancellationTokenSource ctSource = new CancellationTokenSource();
            try
            {
                //if there already is a task Open dont start another one.
                iOpenTasks++;
                if (iOpenTasks < 2)
                {
                    //Start a Task to send the TCP Package, Cancle it 1ms bevore the next Timer tick starts.
                    ctSource.CancelAfter((Convert.ToInt32(ConfigurationManager.AppSettings["refresh"])) - 1);
                    Task task = Task.Run(() => tcpSend(ctSource.Token));
                    await task;
                }
                else
                {
                    iDropedFrames++;
                    updateInfo();
                }
            }
            catch (OperationCanceledException)
            {
                //if the Async task is cancled count the dropped frames and update the Information.
                ctSource.Dispose();
                iDropedFrames++;
                updateInfo();
            }
            catch (Exception)
            {
                ctSource.Dispose();
                disconnected();
            }
            finally
            {
                iOpenTasks--;
            }
        }


        void disconnected()
        {
            if (bConnected)
            {
                //If the Connection to the ESP is broken stop the Timer and show a error Message
                timer1.Enabled = false;
                bConnected = false;
                btnConnect.BackColor = Color.FromArgb(192, 0, 0);
                btnConnect.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 128, 128);
                btnConnect.Text = "Connect to ESP";
                labConnected.Text = "not connected";
                MessageBox.Show("There was a connection problem.\n\nPlease verify the configuration, maybe you should choose a slower picture refresh time.\nAlso verify the power delivery of the Matrix controller.", "Connection problem", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        void updateInfo()
        {
            labConnected.Text = "Connected to\n" + sIP + ":" + iPort + "\n\nMatrix size: " + iLEDwidth + "*" + iLEDheight + "\nMax brightness = " + ConfigurationManager.AppSettings["brightness"] + "\nRefresh: " + ConfigurationManager.AppSettings["refresh"] + "ms\nDropped frames: " + iDropedFrames;
        }



        async Task tcpSend(CancellationToken cToken)
        {
            //The Send task is handled Async to minimize input lag from the GUI
            //Send the created Byte Array ot the Picture as a TCP Packet
            TcpClient client = new TcpClient(sIP, iPort);
            NetworkStream nwStream = client.GetStream();
            nwStream.WriteAsync(bySendRGB565, 0, bySendRGB565.Length, cToken);
            client.Close();
            cToken.ThrowIfCancellationRequested();
        }



        private void btnPopout_Click(object sender, EventArgs e)
        {
            //popout the controlls
            var form = new control();
            form.Location = new Point(this.Right + 10, this.Bottom - form.Height);
            form.Show(this);
        }



        private void Share_SizeChanged(object sender, EventArgs e)
        {
            if (this.Height < 578 && !bControlls)
            {
                btnPopout.PerformClick();
            }
        }



        private void btnResize_Click(object sender, EventArgs e)
        {
            //Reset the Size to the Original window Size
            Size = new Size(927, 578);
        }



        private void btnPdf_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/fabe1999/ScreenShare-WS2812b/blob/master/User%20guide/User-guide.pdf");
        }



        private void Share_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (bConnected)
            {
                try
                {
                    //Send the command to clear the Matrix bevore closing the program
                    TcpClient client = new TcpClient(sIP, iPort);
                    NetworkStream nwStream = client.GetStream();
                    byte[] bytesToSend = new byte[1];
                    bytesToSend[0] = (byte)'X';
                    nwStream.Write(bytesToSend, 0, bytesToSend.Length);
                    client.Close();
                }
                catch (Exception)
                {
                    //if the Matrix is already offline just close the program
                }
            }
        }
    }
}
