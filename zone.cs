using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.UI.Xaml.Media;

namespace minesweeper
{
    class Zone : INotifyPropertyChanged
    {
        /// <summary>
        /// declaration of the PropertyChanged event
        /// used to notify the view that some property has changed
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// actual variable holding content
        /// </summary>
        private string _content;

        /// <summary>
        /// the property controlling access to _content
        /// calls OnPropertyChanged with appropriate argument
        /// </summary>
        public string content
        {
            get
            {
                return _content;
            }
            set 
            {
                _content = value;
                // Call OnPropertyChanged whenever the property is updated
                OnPropertyChanged("content");
            }
        }
        
        /// <summary>
        /// Create the OnPropertyChanged method to raise the event 
        /// </summary>
        /// <param name="name"></param>
        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// extra information recording the index of the zone
        /// </summary>
        public string extra
        {
            get;
            set;
        }

        /// <summary>
        /// whether the zone contains a mine or not
        /// </summary>
        public bool state
        {
            get;
            set;
        }
        
        /// <summary>
        /// whether the user has opened the zone or not
        /// </summary>
        public bool revealed
        {
            get;
            set;
        }
        /*public string color {
            get
            {
                return revealed ? "Gray" : "Transparent";
            }
        }*/

        /// <summary>
        /// actual variable holding _background
        /// </summary>
        private Brush _background;

        /// <summary>
        /// the property controlling access to _background
        /// calls OnPropertyChanged with appropriate argument
        /// </summary>
        public Brush background
        {
            get
            {
                return _background;
            }
            set
            {
                _background = value;
                OnPropertyChanged("background");
            }
        }

        /// <summary>
        /// whether the user has marked the zone as containing a mine
        /// </summary>
        public bool marked
        {
            get;
            set;
        }

        /// <summary>
        /// the number of mines in the surrounding zones
        /// </summary>
        public int zoneValue
        {
            get;
            set;
        }

        /// <summary>
        /// create a zone with the given brush
        /// </summary>
        /// <param name="brush"></param>
        public Zone(Brush brush)
        {
            background = brush;
        }
    }
}
