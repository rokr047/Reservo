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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Xml;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace TheranosWPFChallenge
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        int StartCount = 0;
        String reservationIdCode = "RS";
        int highMaxOccup = 0;
        ObservableCollection<String> reserv;
        String reservationXMLFile = @"reservations.xml";
        String tableXMLFile = @"Tables.xml";

        String curSelNameUnder = "";
        String curSelNumberOfPeople = "";
        String curSelFrom = "";
        int curSelTableId = 0;

        struct Reservation
        {
            public String NumberOfPeople;
            public String NameUnder;
            public String From;
            public int tableId;
            public ACTION type;
        };

        private enum ACTION
        {
            NONE,
            ADD,
            EDIT,
            DELETE
        };

        private enum CLEAR
        {
            ALL,
            RESERVE,
            EDIT
        };

        ACTION curAction = ACTION.NONE;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void listviewReservaionId_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (curAction == ACTION.DELETE)
                return;

            //Retreive information from reservations.xml and populate it in UI
            lblReservationId.Content = "Reservation # : RS" + listviewReservaionId.SelectedValue.ToString().Substring(2);

            XDocument xDoc = XDocument.Load(reservationXMLFile);
            
            var q = from c in xDoc.Descendants("Reservation")
                    where c.Attribute("id").Value == listviewReservaionId.SelectedValue.ToString().Substring(2)
                    select new
                    {
                        tableId = c.Attribute("tableId").Value,
                        numberOfPeople = c.Attribute("numberOfPeople").Value,
                        nameUnder = c.Attribute("nameUnder").Value,
                        _from = c.Attribute("from").Value,
                    };

            foreach (var obj in q)
            {
                lblReservedTable.Content = "Table Reserved : " + obj.tableId.ToString();
                txtEditNumberOfPeople.Text = curSelNumberOfPeople = obj.numberOfPeople.ToString();
                txtEditNameUnder.Text = curSelNameUnder = obj.nameUnder.ToString();
                cmbEditReserveTime.SelectedValue = curSelFrom = obj._from.ToString();
                curSelTableId = Int32.Parse(obj.tableId.ToString());
            }

            lblUpdateDeleteStatus.Content = "update/delete status appears here...";
        }

        private void btnReserve_Click(object sender, RoutedEventArgs e)
        {
            String numberOfPeople = txtNumberOfPeople.Text.Trim();
            String nameUnder = txtNameUnder.Text.Trim();
            String fromTime = cmbReserveTime.SelectedValue.ToString();

            int iFromTime = Int32.Parse(fromTime);

            //Check for mandatory inputs before doing anything else
            if (numberOfPeople == "" || nameUnder == "") 
            {
                lblReservationStatus.Content = "[ERROR] All fields are mandatory!!!";
                return;
            }

            //Check number of people we can accomodate
            if (Int32.Parse(numberOfPeople) > highMaxOccup)
            {
                lblReservationStatus.Content = "[ERROR] Cannot accomodate a party of more than " + highMaxOccup.ToString();
                return;
            }

            Reservation r = new Reservation();
            r.NumberOfPeople = numberOfPeople;
            r.NameUnder = nameUnder;
            r.From = fromTime;
            r.tableId = -1;
            r.type = ACTION.ADD;

            //we can put GetTableid func in background thread because it might take a lot of time if the 
            //number of reservations and the table list are big.
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_GetTableId;
            worker.RunWorkerCompleted += worker_GetTableIdReserveComplete;
            worker.RunWorkerAsync(r);
        }

        private void worker_GetTableIdReserveComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            int numberOfPeople = Int32.Parse(((Reservation)e.Result).NumberOfPeople);
            String fromTime = ((Reservation)e.Result).From;
            String nameUnder = ((Reservation)e.Result).NameUnder;
            int tableID = ((Reservation)e.Result).tableId;

            if (tableID < 0)
            {
                lblReservationStatus.Content = "[ERROR] Could not find a table for reservation. Please try a different time.";
                return;
            }

            //Add reservation Id to listview
            StartCount++;
            string reservationId = "RS" + StartCount;

            //Add reservation data to xml
            XDocument reservationXML = XDocument.Load(reservationXMLFile);

            //id="1" tableId="9" numberOfPeople="6" nameUnder="Mike" from="1000" to="1300"

            reservationXML.Element("Reservations").Add(
                new XElement("Reservation", new XAttribute("id", StartCount), new XAttribute("tableId", tableID), new XAttribute("numberOfPeople", numberOfPeople),
                    new XAttribute("nameUnder", nameUnder), new XAttribute("from", fromTime)
                ));

            reservationXML.Save(reservationXMLFile);

            reserv.Add(reservationId);

            fnClearFields(CLEAR.RESERVE);

            lblReservationStatus.Content = "[SUCCESS] Reservation " + reservationId + " created.";
        }

        private int fnGetTableForCurrentReservation(int numberOfPeople, int fromTime, String nameUnder ,ACTION type)
        {
            //Check if there are any changes, else return the same table id.
            if (type == ACTION.EDIT && numberOfPeople == Int32.Parse(curSelNumberOfPeople) && fromTime == Int32.Parse(curSelFrom) && nameUnder == curSelNameUnder)
            {
                //NO changes, just return the current table id
                return curSelTableId;
            }

            //if only the name has been changed, return the cur table id
            if (type == ACTION.EDIT && numberOfPeople == Int32.Parse(curSelNumberOfPeople) && fromTime == Int32.Parse(curSelFrom) && nameUnder != curSelNameUnder)
            {
                //NO changes, just return the current table id
                return curSelTableId;
            }

            int _tableId = -1;
            //Console.WriteLine(numberOfPeople.ToString() + " " + fromTime.ToString());

            XDocument docReserve = XDocument.Load(reservationXMLFile);
            XDocument docTable = XDocument.Load(tableXMLFile);

            var q = from c in docTable.Descendants("Table")
                    where Int32.Parse(c.Attribute("MaxOccupancy").Value) >= numberOfPeople
                    orderby c.Attribute("MaxOccupancy").Value ascending 
                    select new
                    {
                        tableId = c.Attribute("Id").Value,
                    };

            foreach (var t in q)
            {
                //for each table get the list of reservations
                var res = from r in docReserve.Descendants("Reservation")
                          where Int32.Parse(r.Attribute("tableId").Value) == Int32.Parse(t.tableId)
                          select new
                          {
                              rId = r.Attribute("id").Value,
                              rFrom = r.Attribute("from").Value
                          };

                //Console.WriteLine("Table " + t.tableId + " has " + res.Count() + " reservations.");

                if (res.Count() <= 0) 
                { 
                    //Go greedy, if there is no reservation for the table.
                    //We can reserve this table first.
                    _tableId = Int32.Parse(t.tableId.ToString());
                    break;
                }

                //If we reach this point, this means the table currently has reservations.
                //Try to see if we can fit the current reservation into this table.
                bool setThisTableForReservation = true;
                foreach (var _r in res)
                {
                   //Console.WriteLine("rId : " + _r.rId.ToString() + " from : " + _r.rFrom.ToString());
                    
                    //The reservation we are trying to insert here should not over lap with any of the other
                    //reservations currently for the table.

                    //Assumption: each party will be alloted a table for a max of 1 hour.
                    //Math: if difference btwn resquest reservation and _r.rFrom < 100, reservation not possible.
                    //else reservation is possible
                   if (Math.Abs(Int32.Parse(_r.rFrom) - fromTime) < 100)
                   {
                       setThisTableForReservation = false;
                       break;
                   }

                }

                if (setThisTableForReservation)
                {
                    _tableId = Int32.Parse(t.tableId.ToString());
                    break;
                }
            }

            return _tableId;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //Set up Hour and minute comboboxes
            int sum = 1000;
            bool addHalfHour = true;
            while(sum <= 2100)
            {
                cmbReserveTime.Items.Add(sum.ToString());
                cmbEditReserveTime.Items.Add(sum.ToString());

                if (addHalfHour)
                {
                    sum += 30;
                }
                else
                {
                    sum += 70;
                }                
                addHalfHour = !addHalfHour;
            }

            cmbEditReserveTime.SelectedIndex = cmbReserveTime.SelectedIndex = 0;

            fnLoadTableDataFromXML(tableXMLFile);
            fnLoadReservationData();
        }

        private void fnLoadTableDataFromXML(string xmlFile)
        {
            var xElem = XElement.Load(xmlFile);

            var tables = from table in xElem.Descendants("Table")
                         select table.Attribute("Id").Value;

            //Get the highest max occupancy
            highMaxOccup = xElem.Descendants("Table")
                                .Max(x => (int)x.Attribute("MaxOccupancy"));
        }

        private void fnLoadReservationData()
        {
            var xElem = XElement.Load(reservationXMLFile);

            var reservations = from resr in xElem.Descendants("Reservation")
                               select reservationIdCode + resr.Attribute("id").Value;

            reserv = new ObservableCollection<String>(reservations);

            listviewReservaionId.ItemsSource = reserv;

            if (reserv.Count > 0)
            {
                StartCount = xElem.Descendants("Reservation")
                                .Max(x => (int)x.Attribute("id"));
            }
            else
            {
                StartCount = 0;
            }
        }

        private void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            curAction = ACTION.DELETE;

            XDocument xDoc = XDocument.Load(reservationXMLFile);

            String id = listviewReservaionId.SelectedValue.ToString();

            listviewReservaionId.SelectedIndex = -1;

            xDoc.Root.Elements().Where(x => x.Attribute("id").Value == id.Substring(2)).FirstOrDefault().Remove();

            reserv.Remove("RS" + id.Substring(2));

            xDoc.Save(reservationXMLFile);

            lblUpdateDeleteStatus.Content = "[SUCCESS] "+id + " deleted.";

            fnClearFields(CLEAR.EDIT);

            curAction = ACTION.NONE;
        }

        private void btnDeleteAll_Click(object sender, RoutedEventArgs e)
        {
            curAction = ACTION.DELETE;

            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_DeleteAllReservation;
            worker.RunWorkerCompleted += worker_DeleteAllComplete;
           // worker.RunWorkerAsync();

            XDocument xDoc = XDocument.Load(reservationXMLFile);
            xDoc.Root.Elements().Remove();
            reserv.Clear();
            xDoc.Save(reservationXMLFile);
            listviewReservaionId.SelectedIndex = -1;
            fnClearFields(CLEAR.ALL);
            StartCount = 0;
            lblUpdateDeleteStatus.Content = "[SUCCESS] All reservations have been deleted.";
            curAction = ACTION.NONE;
        }

        private void worker_DeleteAllReservation(object sender, DoWorkEventArgs e)
        {
            XDocument xDoc = XDocument.Load(reservationXMLFile);
            xDoc.Root.Elements().Remove();
            reserv.Clear();
            xDoc.Save(reservationXMLFile);
            listviewReservaionId.SelectedIndex = -1;
            fnClearFields(CLEAR.ALL);
            StartCount = 0;
        }

        private void worker_DeleteAllComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            lblUpdateDeleteStatus.Content = "[SUCCESS] All reservations have been deleted.";
            curAction = ACTION.NONE;
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            //Perform input validation
            String numberOfPeople = txtEditNumberOfPeople.Text.Trim();
            String nameUnder = txtEditNameUnder.Text.Trim();
            String fromTime = cmbEditReserveTime.SelectedValue.ToString();

            //Check for mandatory inputs before doing anything else
            if (numberOfPeople == "" || nameUnder == "")
            {
                lblUpdateDeleteStatus.Content = "[ERROR] All fields are mandatory!!!";
                return;
            }

            if (Int32.Parse(numberOfPeople) > highMaxOccup)
            {
                lblUpdateDeleteStatus.Content = "[ERROR] Cannot accomodate a party of more than " + highMaxOccup.ToString();
                return;
            }

            Reservation r = new Reservation();
            r.NumberOfPeople = numberOfPeople;
            r.NameUnder = nameUnder;
            r.From = fromTime;
            r.tableId = 0;
            r.type = ACTION.EDIT;

            //we can put GetTableid func in background thread because it might take a lot of time if the 
            //number of reservations and the table list are big.
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += worker_GetTableId;
            worker.RunWorkerCompleted += worker_GetTableIdComplete;
            worker.RunWorkerAsync(r);
        }

        private void worker_GetTableId(object sender, DoWorkEventArgs e)
        {
            Reservation _r = (Reservation)e.Argument;
            int tableID = fnGetTableForCurrentReservation(Int32.Parse(_r.NumberOfPeople), Int32.Parse(_r.From), _r.NameUnder ,_r.type);
            _r.tableId = tableID;
            e.Result = _r;
        }

        private void worker_GetTableIdComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            int numberOfPeople = Int32.Parse(((Reservation)e.Result).NumberOfPeople);
            String fromTime = ((Reservation)e.Result).From;
            String nameUnder = ((Reservation)e.Result).NameUnder;
            int tableID = ((Reservation)e.Result).tableId;

            if (tableID < 0)
            {
                lblUpdateDeleteStatus.Content = "[ERROR] Could not update reservation. Tables unavailable for the update.";
                return;
            }

            curAction = ACTION.EDIT;

            XDocument xDoc = XDocument.Load(reservationXMLFile);

            xDoc.Root.Elements().Where(x => x.Attribute("id").Value == listviewReservaionId.SelectedValue.ToString().Substring(2)).FirstOrDefault()
                .SetAttributeValue("tableId", tableID);

            xDoc.Root.Elements().Where(x => x.Attribute("id").Value == listviewReservaionId.SelectedValue.ToString().Substring(2)).FirstOrDefault()
                .SetAttributeValue("numberOfPeople", numberOfPeople);

            xDoc.Root.Elements().Where(x => x.Attribute("id").Value == listviewReservaionId.SelectedValue.ToString().Substring(2)).FirstOrDefault()
                .SetAttributeValue("nameUnder", nameUnder);

            xDoc.Root.Elements().Where(x => x.Attribute("id").Value == listviewReservaionId.SelectedValue.ToString().Substring(2)).FirstOrDefault()
                .SetAttributeValue("from", fromTime);

            xDoc.Save(reservationXMLFile);

            lblUpdateDeleteStatus.Content = "[SUCCESS] " + listviewReservaionId.SelectedValue.ToString() + " updated. New table # " + tableID + ".";

            lblReservedTable.Content = "Table Reserved : " + tableID;

            curAction = ACTION.NONE;
        }

        private void fnClearFields(CLEAR clearType)
        {
            if (clearType == CLEAR.ALL || clearType == CLEAR.RESERVE)
            {
                txtNameUnder.Text = txtNumberOfPeople.Text = "";

                cmbReserveTime.SelectedIndex = 0;

                lblReservationStatus.Content = "reservation status appears here...";
            }
            else if (clearType == CLEAR.ALL || clearType == CLEAR.EDIT)
            {
                lblReservationId.Content = "Reservation # : ---";
                lblReservedTable.Content = "Table Reserved : ---";

                txtEditNameUnder.Text = txtEditNumberOfPeople.Text = "";
                cmbEditReserveTime.SelectedIndex = -1;

                lblUpdateDeleteStatus.Content = "update/delete status appears here...";
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void NameValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^a-zA-Z]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
