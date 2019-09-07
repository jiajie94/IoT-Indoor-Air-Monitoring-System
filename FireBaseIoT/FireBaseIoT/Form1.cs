using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using FireSharp.Config;
using FireSharp.Interfaces;
using FireSharp.Response;

namespace FireBaseIoT
{
    public partial class Form1 : Form
    {
        int count = 1;
        int counter = 0;
        int y1 = 100;
        int y2 = 300;
        int i = 83;

        IFirebaseConfig config = new FirebaseConfig
        {
            AuthSecret = "emWOoUFd2GAB8etSdAJHQXUgqgGv4XcsRJuHyPCd",
            BasePath = "https://smttest-d9e01.firebaseio.com/"
        };

        IFirebaseClient client;

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            client = new FireSharp.FirebaseClient(config);

            if (client!=null)
            {
                MessageBox.Show("Connection is established");
            }

            LiveSensorBtn.BackColor = Color.DimGray;
            button3.Select(); 
            button3.BackColor = Color.LightGray;
        }

        private void LiveSensorBtn_Click(object sender, EventArgs e)
        {
            //set button appearance
            button3.BackColor = Color.DimGray;
            LiveSensorBtn.BackColor = Color.LightGray;

            //show the live graphs
            chart1.Location = new Point(12, 144);
            chart1.Visible = true;
            chart2.Location = new Point(670, 144);
            chart2.Visible = true;
            chart3.Visible = false;
            chart4.Visible = false;

            //hide extra input fields and buttons
            comboBox1.Visible = false;
            comboBox2.Visible = false;
            dateTimePicker1.Visible = false;
            label1.Visible = false;
            label2.Visible = false;
            label4.Visible = false;
            button2.Visible = false;

            if (!serialPort1.IsOpen)
            {
                try
                {
                    serialPort1.Open();

                    Timer timer1 = new Timer
                    {
                        //1 s interval
                        Interval = 1000
                        //1 min interval
                        //Interval = 60000
                        //15 minutes interval
                        //Interval = 900000
                    };

                    timer1.Enabled = true;
                    timer1.Tick += new System.EventHandler(OnTimerEvent);
                }
                catch
                {
                    MessageBox.Show("port not open");
                }
            }
        }
               
        public async void OnTimerEvent(object source, EventArgs e)
        {
            //start reading live sensor data
            if (serialPort1.ReadLine() != null)
            {
                displayData(serialPort1.ReadLine());
            }
        }

        private async void displayData(string output)
        {
            //hide all icons from previous tab
            chart1.Visible = true;
            chart2.Visible = true;
     
            var airObj = new Air { };
            var dustObj = new Dust { };

            //air properties
            String airReading = "";
            String sensorValue = "";
            String airQuality = "";

            //dust properties
            String LPO = "";
            String ratio = "";
            String concentration = "";

            //hardcode the id from firebase here
            ++i;
            String firebaseId = i + "";
            
            try
            {
                //skip first reading as it may not be accurate due to device reading the previous data still (not cleaned)
                if (counter != 0)
                {
                    output = serialPort1.ReadLine();/// -Same as serialPort1.ReadTo("\n");
                    //textBox1.AppendText(output + "\n");

                    int wordCounter = 0;
                    string[] result = output.Split('/');
                    foreach (string value in result)
                    {
                        if (wordCounter == 0)
                        {
                            //air quality
                            int airPollution = value.IndexOf(',');
                            String air = value.Substring(0, airPollution);
                            airReading = air;
                            if (air.Equals("4"))
                            {
                                airQuality = "Fresh air";
                            }
                            else if (air.Equals("3"))
                            {
                                airQuality = "Low pollution";
                            }
                            else if (air.Equals("2"))
                            {
                                airQuality = "High pollution";
                            }
                            else if (air.Equals("1"))
                            {
                                airQuality = "High pollution! Force signal active.";
                            }
                            else
                            {
                                airQuality = "Unknown";
                            }

                            sensorValue = value.Substring(airPollution + 1);

                            airObj = new Air
                            {
                                ID = ++i + "",
                                AirReading = airReading,
                                SensorValue = sensorValue,
                                AirQuality = airQuality,
                                LocationID = "",
                                LocationName = "SIS SR 3-2",
                                Time = DateTime.Now.ToString("HH:mm:ss")
                            };
                        }
                        else
                        {
                            //dust quality in terms of loc, ratio, concentration
                            int slash = value.IndexOf('/');
                            String dustValue = value.Substring(slash + 1);
                            int locNo = dustValue.IndexOf(",");
                            LPO = dustValue.Substring(0, locNo);
                            int r = dustValue.IndexOf(",", locNo + 1);
                            ratio = dustValue.Substring(locNo + 1, 3); //currently hardcode to 3
                            int c = dustValue.LastIndexOf(",");
                            concentration = dustValue.Substring(c + 1);

                            dustObj = new Dust
                            {
                                ID = ++i + "",
                                LPO = LPO,
                                Ratio = ratio,
                                Concentration = concentration,
                                LocationID = "",
                                LocationName = "SIS SR 3-2",
                                Time = DateTime.Now.ToString("HH:mm:ss")
                            };
                        }
                        wordCounter++;
                    }

                    //populate the graphs
                    chart1.Series.FindByName("Air").Points.AddY(Double.Parse(sensorValue));
                    chart2.Series.FindByName("Dust").Points.AddY(Double.Parse(concentration));

                    //send air data
                    SetResponse airResponse = await client.SetTaskAsync("Air/" + firebaseId, airObj);
                    //send dust data
                    SetResponse dustResponse = await client.SetTaskAsync("Dust/" + firebaseId, dustObj);

                    airQuality = "";
                    counter++;
                }
            }
            catch
            {
                MessageBox.Show("There was an error. Please make sure that the correct port was selected, and the device, plugged in.");
            }
            counter++;
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            //Show the graphs for location
            chart1.Visible = false;
            chart2.Visible = false;
            chart3.Visible = true;
            chart3.Series.FindByName("LocationAir").Points.Clear();
            chart4.Visible = true;
            chart4.Series.FindByName("LocationDust").Points.Clear();

            int i = 0;

            while (true)
            {
                i++;
                FirebaseResponse airResponse = await client.GetTaskAsync("Air/" + i);

                if (airResponse.Body == "null")
                {
                    return;
                }

                Air airObj = airResponse.ResultAs<Air>();
                DateTime airTime = DateTime.ParseExact(airObj.Time, "HH:mm:ss", CultureInfo.InvariantCulture);
                chart3.Series.FindByName("LocationAir").Points.AddXY(airTime, airObj.SensorValue);
                
                FirebaseResponse dustResponse = await client.GetTaskAsync("Dust/" + i);

                if (dustResponse.Body == "null")
                {
                    return;
                }

                Dust dustObj = dustResponse.ResultAs<Dust>();
                DateTime dustTime = DateTime.ParseExact(dustObj.Time, "HH:mm:ss", CultureInfo.InvariantCulture);
                chart4.Series.FindByName("LocationDust").Points.AddXY(dustTime, dustObj.Concentration);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //set button appearance
            LiveSensorBtn.BackColor = Color.DimGray;
            button3.BackColor = Color.LightGray;

            //hide the live graphs
            chart1.Visible = false;
            chart2.Visible = false;
            chart3.Visible = true;
            chart4.Visible = true;

            //show extra input fields and buttons
            comboBox1.Visible = true;
            comboBox2.Visible = true;
            dateTimePicker1.Visible = true;
            label1.Visible = true;
            label2.Visible = true;
            label4.Visible = true;
            button2.Visible = true;
        }

        private void chart3_Click(object sender, EventArgs e)
        {

        }

        private void chart1_Click(object sender, EventArgs e)
        {

        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void chart2_Click(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
        
    }
}
