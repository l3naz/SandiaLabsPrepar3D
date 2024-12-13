// Copyright (c) 2010-2022 Lockheed Martin Corporation. All rights reserved.
// Use of this file is bound by the PREPAR3D® SOFTWARE DEVELOPER KIT END USER LICENSE AGREEMENT

//
// Managed Dashboard
//
// Click on Connect to try and connect to a running version of Prepar3D
// Data requests will be made on every second
// NOTE-- Set DEBUG to true to print information to console
// Click on Disconnect to close the connection, and then you should
// be able to click on Connect and restart the process
//

using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using LiveCharts;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Defaults;
using LockheedMartin.Prepar3D.SimConnect;
using System.Runtime.InteropServices;
using LiveCharts.Wpf;
using System.Collections.ObjectModel;
using LiveChartsCore.SkiaSharpView.WinForms;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Linq;
// using Topshelf.Runtime.Windows;



namespace Managed_Dashboard
{
    public partial class Form1 : Form
    {
        private LiveCharts.WinForms.CartesianChart altitude_chart;
        private LiveCharts.WinForms.CartesianChart speed_chart;
        private LiveCharts.WinForms.CartesianChart pb_chart;
        private PolarChart magneticHeadingChart;
        private ObservableCollection<ObservablePolarPoint> magnetic_heading_chart_vals;

        private Panel chartPanel;
        private GroupBox altitudeGroupBox;
        private GroupBox speedGroupBox;
        private GroupBox pbGroupBox;
        private GroupBox magneticHeadingGroupBox;
        private GroupBox gForceGroupBox;

        private Label timeTextBox;
        private Label latitudeTextBox;
        private Label longitudeTextBox;
        private Label timeLabel;
        private Label latitudeLabel;
        private Label longitudeLabel;
        private Label gForceLabel;

        private double prev_time = 0;
        private double counter = 0;
        private const bool DEBUG = false;


        // User-defined win32 event
        const int WM_USER_SIMCONNECT = 0x0402;
        SimConnect simconnect = null;

        // Timer sets requests to 1 second
        Timer requestTimer = new Timer();
        enum DEFINITIONS { Struct1 }

        enum DATA_REQUESTS { REQUEST_1 };

        // this is how you declare a data structure so that
        // simconnect knows how to fill it/read it.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct Struct1
        {
            // this is how you declare a fixed size string
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public String title;
            public double latitude;
            public double longitude;
            public double altitude;
            public double speed;
            public double time;
            public double magnetic_heading;
            public double pitch;
            public double bank;
            public double gForce; // G-force value
        };

        private void InitializeTextBoxes()
        {
            timeLabel = new Label
            {
                Text = "Time:",
                Location = new Point(20, 90),
                Size = new Size(100, 20),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            timeTextBox = new Label
            {
                Location = new Point(20, 105),
                Size = new Size(150, 20),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            latitudeLabel = new Label
            {
                Text = "Latitude:",
                Location = new Point(620, 90),
                Size = new Size(100, 20),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            latitudeTextBox = new Label
            {
                Location = new Point(620, 105),
                Size = new Size(150, 20),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            longitudeLabel = new Label
            {
                Text = "Longitude:",
                Location = new Point(790, 90),
                Size = new Size(100, 20),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            longitudeTextBox = new Label
            {
                Location = new Point(790, 105),
                Size = new Size(150, 20),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };

            Controls.Add(timeLabel);
            Controls.Add(timeTextBox);
            Controls.Add(latitudeLabel);
            Controls.Add(latitudeTextBox);
            Controls.Add(longitudeLabel);
            Controls.Add(longitudeTextBox);
        }


        public Form1()
        {
            InitializeComponent();
            InitializeTextBoxes();

            // Set window to fullscreen with transparency
            this.WindowState = FormWindowState.Normal;
            this.FormBorderStyle = FormBorderStyle.None;
            //this.TopMost = true;
            BringToTop();

            // Transparency for the form background
            this.BackColor = Color.Black;
            this.TransparencyKey = this.BackColor;
            this.Opacity = 0.75;

            // this.chartPanel.Dock = DockStyle.Fill; // Ensure the panel fills the form
            // this.altitude_chart.Dock = DockStyle.Fill; // Ensure charts are fully responsive to resizing


            // Set overlay position to match Prepar3D window
            // SetOverlayOnPrepar3D();

            // Setup charts and controls
            chartPanel = new Panel()
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(chartPanel);

            InitializeAllGroupBox();
            setButtons(true, false);

            requestTimer.Interval = 1000;
            requestTimer.Tick += RequestTimer_Tick;

            // Timer for polling Prepar3D window position
            Timer overlayPositionTimer = new Timer();
            overlayPositionTimer.Interval = 500; // Check window position every 500ms (adjust as needed)
            overlayPositionTimer.Tick += UpdateOverlayPosition;
            overlayPositionTimer.Start();

            // Initialize the charts
            InitializeAltitude();
            InitializeSpeed();
            Initializepb();
            InitializeHeading();
            InitializeGForce(); // Initialize the G-force GroupBox

            altitudeGroupBox.Controls.Add(altitude_chart);
            speedGroupBox.Controls.Add(speed_chart);
            pbGroupBox.Controls.Add(pb_chart);
            magneticHeadingGroupBox.Controls.Add(magneticHeadingChart);
            gForceGroupBox.Controls.Add(gForceLabel);
            
        }

        




        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        //[StructLayout(LayoutKind.Sequential)]

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);


        private const UInt32 SWP_NOSIZE = 0x0001;
        private const UInt32 SWP_NOMOVE = 0x0002;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        private void BringToTop()
        {
            IntPtr handle = this.Handle; // Get the handle of your overlay
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE);

        }

        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }


        private void UpdateOverlayPosition(object sender, EventArgs e)
        {
            // Find the Prepar3D window
            IntPtr hwndPrepar3D = FindWindow(null, "Lockheed Martin® Prepar3D® v5"); // Adjust version as necessary

            if (hwndPrepar3D != IntPtr.Zero)
            {
                // Debug.WriteLine("Found Prepar3D window");
                // Get the position and size of the Prepar3D window
                RECT rect;
                GetWindowRect(hwndPrepar3D, out rect);
                // Debug.WriteLine("Top: {0}", rect.Top);

                // Calculate width and height of the Prepar3D window
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;
                // Debug.WriteLine("Height: {0}", height);

                // Only update if the window is not minimized and the size is valid
                if (width > 0 && height > 0)
                {
                    // Check if the overlay's position and size need to be updated
                    if (this.Top != rect.Top || this.Left != rect.Left || this.Width != rect.Right - rect.Left || this.Height != rect.Bottom - rect.Top)
                    {
                        // Update the overlay's position and size to match the Prepar3D window
                        this.Top = rect.Top;
                        this.Left = rect.Left;
                        this.Width = rect.Right - rect.Left;
                        this.Height = rect.Bottom - rect.Top;


                        this.collapsiblePanel.Location = new System.Drawing.Point(0, 0); // Set at starting position of collapsible panel
                        this.collapsiblePanel.Size = new System.Drawing.Size(this.ClientSize.Width, (int)(this.ClientSize.Height * 0.94)); // Reserve space for buttons at the bottom
                        this.collapsiblePanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; // Anchors to resize dynamically

                        // Update chart sizes to fill the new window dimensions
                        UpdateButtonSizesAndPositions(width, height);
                        UpdateChartSizes(width, height);
                    }
                }
            }
            else
            {
                Debug.WriteLine("Prepar3D window not found.");
            }
        }

        private void UpdateChartSizes(int windowWidth, int windowHeight)
        {
            // Set the size of the group boxes relative to the window size (e.g., 40% width, 20% height)
            int groupBoxWidth = (int)(windowWidth * 0.2); // 20% of the window width
            int groupBoxHeight = (int)(windowHeight * 0.2); // 20% of the window height

            // Calculate the vertical offset to center the group boxes on the left and right sides
            int leftSideY = (windowHeight - (2 * groupBoxHeight) - 20) / 2; // 20 is the total gap between the boxes
            int rightSideY = leftSideY; // Symmetrical for the right side

            // Set the positions for the left-side group boxes (altitude and speed) in the middle
            altitudeGroupBox.Size = new Size(groupBoxWidth, groupBoxHeight);
            altitudeGroupBox.Location = new Point(10, leftSideY); // Middle-left

            speedGroupBox.Size = new Size(groupBoxWidth, groupBoxHeight);
            speedGroupBox.Location = new Point(10, leftSideY + groupBoxHeight + 10); // Directly below the altitude group

            gForceGroupBox.Size = new Size(groupBoxWidth, groupBoxHeight);
            gForceGroupBox.Location = new Point(windowWidth - groupBoxWidth - 10, rightSideY - groupBoxHeight - 10);

            // Set the positions for the right-side group boxes (pitch-bank and magnetic heading) in the middle
            pbGroupBox.Size = new Size(groupBoxWidth, groupBoxHeight);
            pbGroupBox.Location = new Point(windowWidth - groupBoxWidth - 10, rightSideY); // Middle-right

            magneticHeadingGroupBox.Size = new Size(groupBoxWidth, groupBoxHeight);
            magneticHeadingGroupBox.Location = new Point(windowWidth - groupBoxWidth - 10, rightSideY + groupBoxHeight + 10); // Directly below the pitch-bank group

            // Ensure the charts are docked inside the group boxes
            altitude_chart.Dock = DockStyle.Fill;
            speed_chart.Dock = DockStyle.Fill;
            pb_chart.Dock = DockStyle.Fill;
            magneticHeadingChart.Dock = DockStyle.Fill;
            gForceLabel.Dock = DockStyle.Fill;


            // Refresh the layout to apply changes
            chartPanel.Refresh();
            altitudeGroupBox.Refresh();
            speedGroupBox.Refresh();
            pbGroupBox.Refresh();
            magneticHeadingGroupBox.Refresh();
            gForceGroupBox.Refresh();
        }

        private void UpdateButtonSizesAndPositions(int windowWidth, int windowHeight)
        {
            // Calculate button size relative to the window (e.g., 10% width, 5% height)
            int buttonWidth = (int)(windowWidth * 0.1);
            int buttonHeight = (int)(windowHeight * 0.05);

            // Update Report Size and position
            buttonTestReport.Size = new Size(buttonWidth, buttonHeight);
            buttonTestReport.Location = new Point(windowWidth - buttonWidth * 4 - 15, windowHeight - buttonHeight - 10);

            // Update Connect button size and position
            buttonConnect.Size = new Size(buttonWidth, buttonHeight);
            buttonConnect.Location = new Point(windowWidth - buttonWidth * 3 - 15, windowHeight - buttonHeight - 10); // Top-left corner

            // Update Disconnect button size and position
            buttonDisconnect.Size = new Size(buttonWidth, buttonHeight);
            buttonDisconnect.Location = new Point(windowWidth - buttonWidth * 2 - 15, windowHeight - buttonHeight - 10); // Below Connect button

            // Update Toggle Button size and position
            toggleButton.Size = new Size(buttonWidth, buttonHeight);
            toggleButton.Location = new Point(windowWidth - buttonWidth - 15, windowHeight - buttonHeight - 10); // Below Disconnect button

            // Optionally, update other elements (e.g., collapsiblePanel)
            // collapsiblePanel.Location = new System.Drawing.Point(0, toggleButton.Location.Y + toggleButton.Height + 10);
            // collapsiblePanel.Size = new System.Drawing.Size(this.ClientSize.Width, this.ClientSize.Height - (toggleButton.Height + 30));
            // collapsiblePanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Refresh to apply changes
            buttonTestReport.Refresh();
            buttonConnect.Refresh();
            buttonDisconnect.Refresh();
            toggleButton.Refresh();
            collapsiblePanel.Refresh();
        }

        private string filePath;

        public void InitializeCsvLog()
        {
            // Get the current working directory of the application
            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Generate a unique file name with a timestamp
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");  // Format: YYYYMMDD_HHMMSS
            filePath = Path.Combine(currentDirectory, $"flightDataLog_{timestamp}.csv");

            // Create the file and write headers
            using (StreamWriter sw = new StreamWriter(filePath, false))
            {
                sw.WriteLine("Timestamp,Latitude,Longitude,Altitude,Speed,G-Force,MagneticHeading,Pitch,Bank");
            }
        }

        private void LogDataToCsv(Struct1 s1)
        {
            
            if (string.IsNullOrEmpty(filePath))
            {
                InitializeCsvLog();  
            }

            
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Append data to the CSV file
            using (StreamWriter sw = new StreamWriter(filePath, true))
            {
                sw.WriteLine($"{timestamp},{s1.latitude},{s1.longitude},{s1.altitude},{s1.speed},{s1.gForce},{s1.magnetic_heading},{s1.pitch},{s1.bank}");
            }
        }

        public void AddCsvDataToPdfReport()
        {
            string pdfFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FlightReport_WithData.pdf");

            string currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string[] csvFiles = Directory.GetFiles(currentDirectory, "flightDataLog_*.csv");

            if (csvFiles.Length == 0)
            {
                MessageBox.Show("No CSV files found! Ensure flight data has been logged.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string latestCsvFile = csvFiles.OrderByDescending(f => File.GetCreationTime(f)).FirstOrDefault();

            using (FileStream stream = new FileStream(pdfFilePath, FileMode.Create))
            {
                Document document = new Document();
                PdfWriter.GetInstance(document, stream);
                document.Open();

                // Define fonts
                iTextSharp.text.Font titleFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 14, iTextSharp.text.Font.BOLD);
                iTextSharp.text.Font bodyFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10);

                // Add a title
                document.Add(new Paragraph("Flight Report with Data", titleFont));
                document.Add(new Paragraph($"Report Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n", bodyFont));

                // Define the table structure
                PdfPTable table = new PdfPTable(9); 
                table.AddCell("Timestamp");
                table.AddCell("Latitude");
                table.AddCell("Longitude");
                table.AddCell("Altitude");
                table.AddCell("Speed");
                table.AddCell("G-Force");
                table.AddCell("Magnetic Heading");
                table.AddCell("Pitch");
                table.AddCell("Bank");

                // Read data from the latest CSV and populate the table
                using (StreamReader reader = new StreamReader(latestCsvFile))
                {
                    reader.ReadLine();

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] fields = line.Split(',');
                        foreach (string field in fields)
                        {
                            table.AddCell(new PdfPCell(new Phrase(field, bodyFont)));
                        }
                    }
                }

                // Add the table to the document
                document.Add(table);
                document.Close();
            }

            MessageBox.Show($"PDF with data created at: {pdfFilePath}", "PDF Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }





        // Simconnect client will send a win32 message when there is 
        // a packet to process. ReceiveMessage must be called to
        // trigger the events. This model keeps simconnect processing on the main thread.
        protected override void DefWndProc(ref Message m)
        {
            if (m.Msg == WM_USER_SIMCONNECT)
            {
                if (simconnect != null)
                {
                    simconnect.ReceiveMessage();
                }
            }
            else
            {
                base.DefWndProc(ref m);
            }
        }

        private void setButtons(bool bConnect, bool bDisconnect)
        {
            buttonConnect.Enabled = bConnect;
            buttonDisconnect.Enabled = bDisconnect;
        }

        private void closeConnection()
        {
            if (simconnect != null)
            {
                // Dispose serves the same purpose as SimConnect_Close()
                simconnect.Dispose();
                simconnect = null;
                Debug.WriteLine("Connection closed");
            }
        }

        // Set up all the SimConnect related data definitions and event handlers
        private void initDataRequest()
        {
            try
            {
                // listen to connect and quit msgs
                simconnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(simconnect_OnRecvOpen);
                simconnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(simconnect_OnRecvQuit);

                // listen to exceptions
                simconnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(simconnect_OnRecvException);

                // define a data structure
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Title", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Latitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Longitude", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Alt Above Ground", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Ground Velocity", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Absolute Time", "seconds", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Heading Degrees Magnetic", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Pitch Degrees", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "Plane Bank Degrees", "radians", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

                // Add to data definition for G-force
                simconnect.AddToDataDefinition(DEFINITIONS.Struct1, "G FORCE", "GForce", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);


                // IMPORTANT: register it with the simconnect managed wrapper marshaller
                // if you skip this step, you will only receive a uint in the .dwData field.
                simconnect.RegisterDataDefineStruct<Struct1>(DEFINITIONS.Struct1);

                // Request data for "Plane G-Force" along with other data
                // simconnect.RequestDataOnSimObject(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_PERIOD.SIM_FRAME);

                // catch a simobject data request
                simconnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(simconnect_OnRecvSimobjectDataBytype);
            }
            catch (COMException ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        void simconnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Debug.WriteLine("Connected to Prepar3D");
            requestTimer.Start();
        }

        // The case where the user closes Prepar3D
        void simconnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Debug.WriteLine("Prepar3D has exited");
            closeConnection();
        }

        void simconnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Debug.WriteLine("Exception received: " + data.dwException);
        }

        // The case where the user closes the client
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            closeConnection();
        }



        void simconnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {

            switch ((DATA_REQUESTS)data.dwRequestID)
            {
                case DATA_REQUESTS.REQUEST_1:
                    Struct1 s1 = (Struct1)data.dwData[0];
                    TimeSpan ts = TimeSpan.FromSeconds(s1.time);
                    DateTime dateTime = new DateTime(1, 1, 1, 0, 0, 0) + ts;
                    double angleInDegrees = RadiansToDegrees(s1.magnetic_heading);


                    // Only update charts if the time has updated.
                    // Time will not update if simulation is paused.
                    if (prev_time != s1.time)
                    {
                        Debug.WriteLine("Title: " + s1.title);
                        Debug.WriteLine("Lat:   " + s1.latitude);
                        Debug.WriteLine("Lon:   " + s1.longitude);
                        Debug.WriteLine("Alt:   " + s1.altitude);
                        Debug.WriteLine("Speed: " + s1.speed);
                        Debug.WriteLine("Time: " + dateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                        Debug.WriteLine("Time: " + s1.time);
                        Debug.WriteLine("Magnetic heading: " + s1.magnetic_heading);
                        Debug.WriteLine("Pitch: " + s1.pitch);
                        Debug.WriteLine("Bank: " + s1.bank);
                        Debug.WriteLine("G-force: " + s1.gForce);

                        // Send info to ChartForm
                        altitude_chart.Series[0].Values.Add(s1.altitude);
                        speed_chart.Series[0].Values.Add(s1.speed);
                        pb_chart.Series[0].Values.Add(s1.pitch);
                        pb_chart.Series[1].Values.Add(s1.bank);

                        // Update both points to form a line at the new angle
                        magnetic_heading_chart_vals[0] = new ObservablePolarPoint(angleInDegrees, 0);   // Point at center
                        magnetic_heading_chart_vals[1] = new ObservablePolarPoint(angleInDegrees, 100); // Point at max radius
                        // magnetic_heading_chart_vals.Add(new ObservablePolarPoint(RadiansToDegrees(s1.magnetic_heading), counter));


                        // Update text boxes with new data
                        timeTextBox.Text = dateTime.ToString("yyyy-MM-dd HH:mm:ss");
                        latitudeTextBox.Text = s1.latitude.ToString("F6");
                        longitudeTextBox.Text = s1.longitude.ToString("F6");
                        gForceLabel.Text = $"G-force: {s1.gForce:F2} Gs";

                        // Log data to CSV
                        LogDataToCsv(s1);
 

                        prev_time = s1.time;
                        counter += 1;
                    }
                    break;

                default:
                    Debug.WriteLine("Unknown request ID: " + data.dwRequestID);
                    break;
            }
        }

        private double RadiansToDegrees(double x)
        {
            x *= (180 / Math.PI);
            if (x < 0) x += 360;
            return x;
        }

        private void InitializeGForce()
        {

            // Initialize the G-force label
            gForceLabel = new Label
            {
                Text = "G-force:",
                Location = new Point(520, 100),
                Size = new Size(1000, 100),
                ForeColor = Color.White,
                BackColor = Color.Transparent
            };
        }

        private void InitializeAllGroupBox()
        {
            altitudeGroupBox = new GroupBox()
            {
                Text = "Altitude Chart",
                Location = new System.Drawing.Point(10, 70),
                Size = new System.Drawing.Size(500, 350),

            };

            speedGroupBox = new GroupBox()
            {
                Text = "Speed Chart",
                Location = new System.Drawing.Point(10, 430),
                Size = new System.Drawing.Size(500, 350),
            };

            pbGroupBox = new GroupBox()
            {
                Text = "Pitch, Bank",
                Location = new System.Drawing.Point(520, 430),
                Size = new System.Drawing.Size(500, 350),

            };

            magneticHeadingGroupBox = new GroupBox
            {
                Text = "Magnetic Heading",
                Location = new Point(520, 70),
                Size = new Size(500, 350),
            };

            gForceGroupBox = new GroupBox
            {
                Text = "G-Force",
                Location = new Point(520, 70),
                Size = new Size(500, 80),
            };

            chartPanel.Controls.Add(altitudeGroupBox);
            chartPanel.Controls.Add(speedGroupBox);
            chartPanel.Controls.Add(gForceGroupBox);
            chartPanel.Controls.Add(pbGroupBox);
            chartPanel.Controls.Add(magneticHeadingGroupBox);
        }

        private void InitializeAltitude()
        {
            // Create a new Cartesian chart
            altitude_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            altitude_chart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Time (seconds)",
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            altitude_chart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Altitude Above Ground (feet)",
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var altitudeSeries = new LineSeries
            {
                Title = "Altitude",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 5,
            };
            altitude_chart.Zoom = ZoomingOptions.X;
            altitude_chart.Pan = PanningOptions.X;
            altitude_chart.Series.Add(altitudeSeries);
        }

        private void InitializeSpeed()
        {
            // Create a new Cartesian chart
            speed_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            speed_chart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Time (seconds)",
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define Y axis
            speed_chart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Speed (knots)",
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var speedSeries = new LineSeries
            {
                Title = "Speed",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 5,
            };
            speed_chart.Zoom = ZoomingOptions.X;
            speed_chart.Pan = PanningOptions.X;
            speed_chart.Series.Add(speedSeries);
        }

        private void Initializepb()
        {
            // Create a new Cartesian chart
            pb_chart = new LiveCharts.WinForms.CartesianChart
            {
                Dock = DockStyle.Fill
            };

            // Define X axis
            pb_chart.AxisX.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Time (seconds)",
                LabelFormatter = value => value.ToString("N3"), // Optional formatting for axis labels
            });

            // Define Y axis
            pb_chart.AxisY.Add(new LiveCharts.Wpf.Axis
            {
                Title = "Radians",
                LabelFormatter = value => value.ToString("0"), // Optional formatting for axis labels
            });

            // Define a new LineSeries for altitude data
            var pitchSeries = new LineSeries
            {
                Title = "Pitch",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 5,
            };

            var bankSeries = new LineSeries
            {
                Title = "Bank",
                Values = new ChartValues<double>(), // Initialize empty chart values
                PointGeometry = DefaultGeometries.Circle,
                PointGeometrySize = 5,
            };

            // Add the series to the chart
            pb_chart.LegendLocation = LegendLocation.Bottom; // Change this to control legend location
            pb_chart.Zoom = ZoomingOptions.X;
            pb_chart.Pan = PanningOptions.X;
            pb_chart.Series.Add(pitchSeries);
            pb_chart.Series.Add(bankSeries);
        }

        private void InitializeHeading()
        {
            magneticHeadingChart = new PolarChart();
            magnetic_heading_chart_vals = new ObservableCollection<ObservablePolarPoint> 
            {
                new ObservablePolarPoint(0, 0),   // Point at center with radius 0
                new ObservablePolarPoint(0, 100)  // Point at maximum radius (adjust as needed)
            };

            magneticHeadingChart.Series = new[]
            {
                new PolarLineSeries<ObservablePolarPoint>
                {
                    Values = magnetic_heading_chart_vals,
                    Fill = null,
                    IsClosed = false,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint(SKColors.Red)
                    {
                        StrokeThickness = 5,
                    },
                }
            };

            magneticHeadingChart.AngleAxes = new[]
            {
                new PolarAxis
                {
                    TextSize = 18,
                    LabelsPaint = new SolidColorPaint(SKColors.Green),
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
                    {
                        StrokeThickness = 1,
                        PathEffect = new DashEffect(new float[] {5, 5})
                    },
                    MinLimit = 0,
                    MaxLimit = 360,
                    Labeler = angle =>
                    {
                        if (angle == 0)
                            return "N";
                        else if (angle == 90)
                            return "E";
                        else if (angle == 180)
                            return "S";
                        else if (angle == 270)
                            return "W";
                        else
                            return ""; // Empty label for other angles
                    },
                    ForceStepToMin = true,
                    MinStep = 90, // Set 90-degree intervals to show only the cardinal labels

                }
            };

            magneticHeadingChart.RadiusAxes = new[]
            {
                new PolarAxis
                {
                    TextSize = 10,
                    LabelsPaint = new SolidColorPaint(SKColors.Blue),
                    SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray) { StrokeThickness = 1 },
                    MinLimit = 0,
                    MaxLimit = 100,
                    Labeler = value => (value == 50 || value == 100) ? "" : value.ToString(), // Remove 50 and 100 labels
                }
            };
            magneticHeadingGroupBox.Controls.Add(magneticHeadingChart);

        }

        private void TogglePanelsButton_Click(object sender, EventArgs e)
        {
            // Toggle visibility of the collapsible panel
            if (collapsiblePanel.Visible)
            {
                collapsiblePanel.Visible = false;
                toggleButton.Text = "Hide dashboard";
            }
            else
            {
                collapsiblePanel.Visible = true;
                toggleButton.Text = "Show dashboard";
            }
        }

        private void buttonTestReport_Click(object sender, EventArgs e)
        {
            AddCsvDataToPdfReport();
        }




        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (simconnect == null)
            {
                try
                {
                    // the constructor is similar to SimConnect_Open in the native API4
                    simconnect = new SimConnect("Managed Dashboard", this.Handle, WM_USER_SIMCONNECT, null, 0);
                    setButtons(false, true);
                    initDataRequest();
                    InitializeCsvLog();
                }
                catch (COMException ex)
                {
                    Debug.WriteLine("Unable to connect to Prepar3D:\n\n" + ex.Message);
                }
            }
            else
            {
                Debug.WriteLine("Error - try again");
                closeConnection();
                setButtons(true, false);
            }
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            // clear all the current values from the graphs
            altitude_chart.Series[0].Values.Clear();
            speed_chart.Series[0].Values.Clear();
            pb_chart.Series[0].Values.Clear();
            pb_chart.Series[1].Values.Clear();
            magnetic_heading_chart_vals.Clear();
            closeConnection();
            setButtons(true, false);
        }

        private void RequestTimer_Tick(object sender, EventArgs e)
        {
            // Send data request every second
            if (simconnect != null)
            {
                simconnect.RequestDataOnSimObjectType(DATA_REQUESTS.REQUEST_1, DEFINITIONS.Struct1, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
                Debug.WriteLine("Request sent...");
            }
        }

    }
}
