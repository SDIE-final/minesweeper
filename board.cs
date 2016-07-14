using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Collections.ObjectModel;
using System.ComponentModel;

using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;

namespace minesweeper
{
    class Board : INotifyPropertyChanged
    {
        /// <summary>
        /// declaration of the delegate
        /// </summary>
        public delegate void SomethingHappened();

        /// <summary>
        /// notifies the mainpage that the game is done
        /// </summary>
        public SomethingHappened win;

        /// <summary>
        /// notifies the mainpage that the game is failed
        /// </summary>
        public SomethingHappened lose;

        /// <summary>
        /// declaration of the PropertyChanged event
        /// used to notify the view that some property has changed
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

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
            else
            {
                // don't know how to handle this situation
            }
        }

        /// <summary>
        /// the height and width of the board
        /// supposed to be read-only but not supported by C#
        /// </summary>
        public vector[] actualBoardSize = new vector[]
        {
            new vector(9, 9),
            new vector(16, 16),
            new vector(16, 30)
        };

        /// <summary>
        /// the direction vector of all possible neighbours
        /// supposed to be read-only but not supported by C#
        /// </summary>
        private vector[] neighbours = new vector[]
        {
            new vector(-1, -1),
            new vector(-1, 0),
            new vector(-1, 1),
            new vector(0, -1),
            new vector(0, 1),
            new vector(1, -1),
            new vector(1, 0),
            new vector(1, 1)
        };

        /// <summary>
        /// number of mines in the board
        /// indice corresponds to different difficulties
        /// can't use const for reference type...
        /// </summary>
        private readonly int[] _numMines =
        {
            10,
            40,
            99
        };

        /// <summary>
        /// property that makes _numMines readonly
        /// </summary>
        private int numMines
        {
            get
            {
                return _numMines[(int)currentBoardSize];
            }
        }

        /// <summary>
        /// a random number generator for deciding the location of mines
        /// </summary>
        private Random rand = new Random();

        /// <summary>
        /// enum type for possible states of the board
        /// </summary>
        public enum state
        {
            playing,
            waiting
        };

        /// <summary>
        /// enum type for possible sizes of the board
        /// </summary>
        public enum size
        {
            small,
            medium,
            big
        };

        /// <summary>
        /// a two-dimensional array containing all the zones
        /// </summary>
        private Zone[,] zones;

        /// <summary>
        /// a list containing all the zones
        /// </summary>
        public List<Zone> zoneList;

        /// <summary>
        /// the variable holding the current state of the board
        /// </summary>
        private state _currentBoardState;

        /// <summary>
        /// the property controlling access to _currentBoardState
        /// </summary>
        public state currentBoardState
        {
            get
            {
                return _currentBoardState;
            }
            private set
            {
                _currentBoardState = value;
            }
        }

        /// <summary>
        /// the variable holding the current size of the board
        /// </summary>
        private size _currentBoardSize;

        /// <summary>
        /// the property controlling access to _currentBoardSize
        /// calls OnPropertyChanged with appropriate argument
        /// </summary>
        public size currentBoardSize 
        { 
            get
            {
                return _currentBoardSize;
            }
            private set
            {
                _currentBoardSize = value;
                // **magic here**
                // if I preserve this two lines,
                // the value of progressBar will be wrong
                // when currentBoardSize is changed
                // I have no idea why so I left it here
                // OnPropertyChanged("numRevealedSafe");
                // OnPropertyChanged("numMarkedMines");
                OnPropertyChanged("numTotalZone");
            }
        }

        /// <summary>
        /// number of remaining safe zones
        /// </summary>
        private int _numUnrevealedSafe;

        /// <summary>
        /// the property controlling access to _numUnrevealedSafe
        /// calls OnPropertyChanged with appropriate argument
        /// </summary>
        public int numUnrevealedSafe 
        {
            get
            {
                return _numUnrevealedSafe;
            }
            private set
            {
                _numUnrevealedSafe = value;
                OnPropertyChanged("numRevealedSafe");
            }
        }

        /// <summary>
        /// number of remaining dangerous zones
        /// </summary>
        private int _numUnmarkedMines;

        /// <summary>
        /// the property controlling access to _numUnmarkedMines
        /// calls OnPropertyChanged with appropriate argument
        /// </summary>
        public int numUnmarkedMines
        {
            get
            {
                return _numUnmarkedMines;
            }
            private set
            {
                _numUnmarkedMines = value;
                OnPropertyChanged("numUnmarkedMines");
                OnPropertyChanged("numMarkedMines");
            }
        }

        /// <summary>
        /// duplicate information for displaying progress bar
        /// uses dependency to notify change
        /// </summary>
        public int numRevealedSafe
        {
            get
            {
                return numTotalZone - numMines - numUnrevealedSafe;
            }
        }

        /// <summary>
        /// duplicate information for displaying progress bar
        /// uses dependency to notify change
        /// </summary>
        public int numMarkedMines
        {
            get
            {
                return numMines - numUnmarkedMines;
            }
        }

        /// <summary>
        /// total number of zones
        /// </summary>
        public int numTotalZone
        {
            get
            {
                return actualBoardSize[(int)currentBoardSize].x
                        * actualBoardSize[(int)currentBoardSize].y;
            }
        }

        /// <summary>
        /// grayBrush: zone with no beighbouring mine
        /// transparentBrush: normal zone
        /// </summary>
        static private SolidColorBrush grayBrush, transparentBrush;

        /// <summary>
        /// flagBrush: marked zone
        /// mineBrush: zone with a mine in it
        /// wrongMarkBrush: zone  with a mine in it but is marked wrongly
        /// </summary>
        static private ImageBrush flagBrush, mineBrush, wrongMarkBrush;

        /// <summary>
        /// initializes the board
        /// </summary>
        public Board()
        {
            // set state
            currentBoardState = state.waiting;

            // add delegate
            lose = revealAllMines;

            // initialize background brush(pure color and image)
            grayBrush = new SolidColorBrush(Windows.UI.Colors.Gray);
            transparentBrush = new SolidColorBrush();
            flagBrush = newImageBrushFromFile("ms-appx:///Assets/flag.png");
            mineBrush = newImageBrushFromFile("ms-appx:///Assets/mine.png");
            wrongMarkBrush = newImageBrushFromFile("ms-appx:///Assets/wrong mark.png");
        }

        /// <summary>
        /// helper function to create an ImageBrush using a filename
        /// </summary>
        /// <param name="filename">filename of the image</param>
        /// <returns></returns>
        private ImageBrush newImageBrushFromFile(string filename)
        {
            ImageBrush newImageBrush = new Windows.UI.Xaml.Media.ImageBrush();
            newImageBrush.ImageSource = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri(filename));
            newImageBrush.AlignmentX = AlignmentX.Center;
            newImageBrush.AlignmentY = AlignmentY.Center;
            newImageBrush.Stretch = Stretch.UniformToFill;
            return newImageBrush;
        }
        
        /// <summary>
        /// test if a position is in the board
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        private bool isValid(vector v)
        {
            return v.x >= 0 && v.x < actualBoardSize[(int)currentBoardSize].x
                    && v.y >= 0 && v.y < actualBoardSize[(int)currentBoardSize].y;
        }
        
        /// <summary>
        /// changes size of the board
        /// called when difficulty changes
        /// </summary>
        /// <param name="newBoardSize"></param>
        public void setBoardSize(size newBoardSize)
        {
            currentBoardState = state.waiting;
            currentBoardSize = newBoardSize;
            reset();
        }

        /// <summary>
        /// resets the whole board according to current boardSize
        /// </summary>
        public void reset()
        {
            resetZones();
            randomPlaceMines();
            storeToList();
            currentBoardState = state.playing;
        }
        
        /// <summary>
        /// allocates new zones for the board
        /// thanks to C#'s GC
        /// </summary>
        private void resetZones()
        {
            zones = new Zone[actualBoardSize[(int)currentBoardSize].x, actualBoardSize[(int)currentBoardSize].y];
            numUnmarkedMines = numMines;
            numUnrevealedSafe = numTotalZone - numMines;
            // reset all zones
            for (int i = 0; i < actualBoardSize[(int)currentBoardSize].x; ++i)
                for (int j = 0; j < actualBoardSize[(int)currentBoardSize].y; ++j)
                {
                    zones[i, j] = new Zone(transparentBrush);
                    zones[i, j].extra = (i * actualBoardSize[(int)currentBoardSize].y + j).ToString();
                }
        }

        /// <summary>
        /// places mines on the board randomly
        /// </summary>
        private void randomPlaceMines()
        {
            for (int i = 0; i < numMines; ++i)
            {
                int toSetX, toSetY;
                do
                {
                    toSetX = rand.Next(actualBoardSize[(int)currentBoardSize].x);
                    toSetY = rand.Next(actualBoardSize[(int)currentBoardSize].y);
                } while (zones[toSetX, toSetY].state == true);
                placeMine(new vector(toSetX, toSetY));
            }
        }

        /// <summary>
        /// places a bomb and updates its neighbour
        /// </summary>
        /// <param name="target"></param>
        private void placeMine(vector target)
        {
            zones[target.x, target.y].state = true;
            for (int i = 0; i < neighbours.Length; ++i)
                if (isValid(new vector(target.x + neighbours[i].x, target.y + neighbours[i].y)))
                    zones[target.x + neighbours[i].x, target.y + neighbours[i].y].zoneValue += 1;
        }

        /// <summary>
        /// copy to zone_list
        /// </summary>
        private void storeToList()
        {
            zoneList = new List<Zone> { };
            for (int i = 0; i < actualBoardSize[(int)currentBoardSize].x; ++i)
                for (int j = 0; j < actualBoardSize[(int)currentBoardSize].y; ++j)
                    zoneList.Add(zones[i, j]);
        }

        /// <summary>
        /// open a zone
        /// trigger appropriate event(s)
        /// </summary>
        /// <param name="zoneToOpen"></param>
        public void open(int zoneToOpen)
        {
            if (currentBoardState == state.playing)
            { 
                int x = zoneToOpen / actualBoardSize[(int)currentBoardSize].y,
                    y = zoneToOpen % actualBoardSize[(int)currentBoardSize].y;
                if (!zones[x, y].revealed && !zones[x, y].marked)
                {
                    zones[x, y].revealed = true;
                    zoneList[zoneToOpen].background = grayBrush;
                    if (zones[x, y].state)
                    {
                        // lose
                        zoneList[zoneToOpen].revealed = true;
                        zoneList[zoneToOpen].background = mineBrush;
                        currentBoardState = state.waiting;
                        lose();
                        return;
                    }
                    if (zones[x, y].zoneValue != 0)
                    {
                        zoneList[zoneToOpen].content = zones[x, y].zoneValue.ToString();
                    }
                    else
                    {
                        // zoneValue == 0, auto open
                        for (int i = 0; i < neighbours.Length; ++i)
                        {
                            if (isValid(new vector(x + neighbours[i].x, y + neighbours[i].y)))
                            {
                                open(zoneToOpen + neighbours[i].x * actualBoardSize[(int)currentBoardSize].y + neighbours[i].y);
                            }
                        }
                    }
                    --numUnrevealedSafe;
                    if (numUnrevealedSafe == 0)
                    {
                        // win
                        currentBoardState = state.waiting;
                        win();
                    }
                    return;
                }
            }
            return;
        }

        /// <summary>
        /// mark a zone
        /// update related properties
        /// </summary>
        /// <param name="zoneToMark"></param>
        public void mark(int zoneToMark)
        {
            if (currentBoardState == state.playing)
            {
                int x = zoneToMark / actualBoardSize[(int)currentBoardSize].y,
                    y = zoneToMark % actualBoardSize[(int)currentBoardSize].y;
                if (!zones[x, y].revealed)
                {
                    zones[x, y].marked = !zones[x, y].marked;
                    if (zones[x, y].marked)
                    {
                        zoneList[zoneToMark].background = flagBrush;
                    }
                    else
                    {
                        zoneList[zoneToMark].background = transparentBrush;
                    }
                    numUnmarkedMines += zones[x, y].marked ? -1 : 1;
                }
            }
        }

        /// <summary>
        /// opens all unmarked neighbouring zone
        /// called by explore
        /// </summary>
        private void revealAllMines()
        {
            for (int i = 0; i < actualBoardSize[(int)currentBoardSize].x; ++i)
            {
                for (int j = 0; j < actualBoardSize[(int)currentBoardSize].y; ++j)
                {
                    if (zones[i, j].state && !zones[i, j].marked)
                    {
                        zoneList[i * actualBoardSize[(int)currentBoardSize].y + j].background = mineBrush;
                    }
                }
            }
        }

        /// <summary>
        /// called when user double clicks a unmarked zone
        /// if all neighbouring unmarked zones are safe, open them
        /// if any neighbouring marked zones are safe, the game fails
        /// </summary>
        /// <param name="zoneToExplore"></param>
        public void explore(int zoneToExplore)
        {
            if (currentBoardState == state.playing)
            {
                int x = zoneToExplore / actualBoardSize[(int)currentBoardSize].y,
                    y = zoneToExplore % actualBoardSize[(int)currentBoardSize].y;
                if (zones[x, y].revealed)
                {
                    bool safe = true;
                    for (int i = 0; i < neighbours.Length; ++i)
                    {
                        if (isValid(new vector(x + neighbours[i].x, y + neighbours[i].y)))
                        {
                            Zone targetZone = zones[x + neighbours[i].x, y + neighbours[i].y];
                            if (!targetZone.revealed)
                            {
                                if (targetZone.state && !targetZone.marked)
                                {
                                    safe = false;
                                }
                                // wrong mark
                                if (targetZone.marked && !targetZone.state)
                                {
                                    currentBoardState = state.waiting;
                                    zoneList[int.Parse(targetZone.extra)].background = wrongMarkBrush;
                                    // lose
                                    lose();
                                }
                            }
                        }
                    }
                    if (safe)
                    {
                        for (int i = 0; i < neighbours.Length; ++i)
                        {
                            if (isValid(new vector(x + neighbours[i].x, y + neighbours[i].y)))
                            {
                                Zone targetZone = zones[x + neighbours[i].x, y + neighbours[i].y];
                                if (!targetZone.revealed)
                                {
                                    open(int.Parse(targetZone.extra));
                                }
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// 将整个board的状态序列化
        /// 用来发给客户端
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string result = "";
            for (int i = 0; i < actualBoardSize[(int)currentBoardSize].x; ++i)
            {
                for (int j = 0; j < actualBoardSize[(int)currentBoardSize].y; ++j)
                {
                    result += zones[i, j].state ? "1" : "0";
                }
            }
            return result;
        }
        
        /// <summary>
        /// 接收地图
        /// </summary>
        /// <param name="source"></param>
        public void FromString(string source)
        {
            currentBoardState = state.waiting;
            for (int i = 0; i <= (int)size.big; ++i)
            {
                if (source.Length == actualBoardSize[i].x * actualBoardSize[i].y)
                {
                    currentBoardSize = (size)i;
                }
            }
            resetZones();
            for (int i = 0; i < actualBoardSize[(int)currentBoardSize].x; ++i)
            {
                for (int j = 0; j < actualBoardSize[(int)currentBoardSize].y; ++j)
                {
                    if (source.ElementAt(i * actualBoardSize[(int)currentBoardSize].y + j) == '1')
                    {
                        placeMine(new vector(i, j));
                    }
                }
            }
            storeToList();
            currentBoardState = state.playing;
        }
    }
}
