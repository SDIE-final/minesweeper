﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.UI.Popups;
using System.ComponentModel;

using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.UI.Xaml.Media.Animation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace minesweeper
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        /// <summary>
        /// 服务端 socket
        /// </summary>
        private StreamSocketListener _listener;

        /// <summary>
        /// 客户端 socket
        /// </summary>
        private StreamSocket _client;

        /// <summary>
        /// 客户端向服务端发送数据时的 DataWriter
        /// </summary>
        private DataWriter _writer;

        /// <summary>
        /// enum type of possible statuses of the state machine
        /// </summary>
        enum Status
        {
            LocalWaiting,
            LocalPlaying,
            NetListening,
            NetConnected,
            NetReady,
            NetStarting,
            NetPlaying
        };

        /// <summary>
        /// the actual status variable
        /// </summary>
        private Status _gameStatus;

        /// <summary>
        /// property to access status
        /// used to hide and show the second progressBar
        /// </summary>
        private Status gameStatus
        {
            get
            {
                return _gameStatus;
            }
            set
            {
                if (_gameStatus.ToString().StartsWith("Local") && value.ToString().StartsWith("Net"))
                {
                    componentMarkedProgressBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    componentRevealedProgressBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
                    componentTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
                }
                else if (_gameStatus.ToString().StartsWith("Local") && value.ToString().StartsWith("Net"))
                {
                    componentMarkedProgressBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    componentRevealedProgressBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                    componentTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                }
                _gameStatus = value;
            }
        }

        /// <summary>
        /// enum type of possible difficulties of the game
        /// </summary>
        enum Difficulty
        {
            Low,
            Intermediate,
            High
        };

        /// <summary>
        /// current difficulty of the game
        /// </summary>
        private Difficulty gameDifficulty;

        /// <summary>
        /// strings corresponding to different diffculties 
        /// used to compose highscore message
        /// </summary>
        private readonly string[] difficultyString = { "低级", "中级", "高级" };

        /// <summary>
        /// the board that contains all zones
        /// implemented in a separating class
        /// </summary>
        private Board board;

        /// <summary>
        /// timers for countdown and update elapsed time string
        /// both used in online playing
        /// </summary>
        private DispatcherTimer countdownTimer, gameElapsedTimer;

        /// <summary>
        /// time remaining before the game starts
        /// used for online playing to let both players get ready
        /// </summary>
        private int _countDown;

        /// <summary>
        /// status used for online playing
        /// -1 means failed
        /// 0 means normal
        /// 1 means done
        /// </summary>
        private int finished;

        /// <summary>
        /// the start time of this game(not application)
        /// </summary>
        private DateTime startTime;

        /// <summary>
        /// total time used in this game
        /// caculated from startTime
        /// </summary>
        private TimeSpan elapsedTime;

        /// <summary>
        /// the filename to store persistent data
        /// the data is store in local app storage
        /// note that rebuilding the app ruins it
        /// </summary>
        const string DatafileName = "high score.dat";

        /// <summary>
        /// the file object of the datafile
        /// provides read and write operations
        /// </summary>
        private StorageFile dataFile;

        /// <summary>
        /// pure colour brushes
        /// used to display current difficulty
        /// each difficulty has two states, light and dark
        /// 3 * 2 = 6 brushes in total
        /// </summary>
        static private Brush
            lowDarkBrush
          , lowLightBrush
          , intermediateDarkBrush
          , intermediateLightBrush
          , highDarkBrush
          , highLightBrush
        ;

        /// <summary>
        /// the highest score of low, intermediate, high diffculty, respectively
        /// -1 represents that there is not yet a score
        /// reads from storage at start of the application
        /// writes back when the application is closed
        /// </summary>
        private int[] highscores;

        /// <summary>
        /// initializes the UI, some necessary setup
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            board = new Board();
            gameStatus = Status.LocalWaiting;
            gameDifficulty = Difficulty.Low;
            changeSize(Board.size.small);
            refreshBoardView();

            // set data binding
            minesLeft.DataContext = board;
            selfRevealedProgressBar.DataContext = board;
            selfMarkedProgressBar.DataContext = board;
            componentRevealedProgressBar.DataContext = board;
            componentMarkedProgressBar.DataContext = board;

            // add win and lose delegate
            board.win += winHandler;
            board.lose += loseHandler;

            // initialize countDown_dispatcherTimer
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = new TimeSpan(0, 0, 0, 1);
            countdownTimer.Tick += countdownTick;
            gameElapsedTimer = new DispatcherTimer();
            gameElapsedTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            gameElapsedTimer.Tick += gameElapsedTick;

            // set for share
            DataTransferManager.GetForCurrentView().DataRequested += MainPage_DataRequested;

            // initialize brush for difficuly button
            lowDarkBrush = new SolidColorBrush(Windows.UI.Colors.DarkGreen);
            lowLightBrush = new SolidColorBrush(Windows.UI.Colors.GreenYellow);
            intermediateDarkBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xCE, 0xB2, 0x00));
            intermediateLightBrush = new SolidColorBrush(Windows.UI.Colors.Yellow);
            highDarkBrush = new SolidColorBrush(Windows.UI.Colors.DarkRed);
            highLightBrush = new SolidColorBrush(Windows.UI.Colors.Tomato);

            readFromDataFile();

            // hack
            board.PropertyChanged += refreshProgressBar;
        }

        /// <summary>
        /// reads data from datafile
        /// </summary>
        private async void readFromDataFile()
        {
            // setup local data
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            bool fileExists = true;
            try
            {
                dataFile = await localFolder.GetFileAsync(DatafileName);
            }
            catch
            {
                fileExists = false;
            }
            if (!fileExists)
            {
                dataFile = await localFolder.CreateFileAsync(DatafileName);
                highscores = new int[] { -1, -1, -1 };
                await FileIO.WriteTextAsync(dataFile, getBase64HighScoreString());
            }
            else
            {
                // use Base64 to prevent simple hack
                string strIn = await FileIO.ReadTextAsync(dataFile);
                IBuffer buffFromBase64;
                try
                {
                    buffFromBase64 = CryptographicBuffer.DecodeFromBase64String(strIn);
                }
                catch
                {
                    messageBox("数据文件损坏，最高分纪录将被重置!");
                    highscores = new int[] { -1, -1, -1 };
                    writeToDataFile();
                    return;  // if I use a bool instead, VS will think buffFromBase64 is used uninitialized...
                }
                String highscoreString = CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, buffFromBase64);
                string[] highscoreStrings = highscoreString.Split(new char[] { '|' });
                if (highscoreStrings.Count() != 3)
                {
                    messageBox("数据文件损坏，最高分纪录将被重置!");
                    highscores = new int[] { -1, -1, -1 };
                    await FileIO.WriteTextAsync(dataFile, getBase64HighScoreString());
                }
                else
                {
                    highscores = new int[] {
                        int.Parse(highscoreStrings[0]),
                        int.Parse(highscoreStrings[1]),
                        int.Parse(highscoreStrings[2])
                    };
                }
            }
        }

        /// <summary>
        /// constructs the highscoreString and encodes it with Base64
        /// </summary>
        /// <returns>the Base64 string</returns>
        private string getBase64HighScoreString()
        {
            string originHighScoreString = highscores[0].ToString()
                                            + "|" + highscores[1].ToString()
                                            + "|" + highscores[2].ToString();
            IBuffer buffUTF8 = CryptographicBuffer.ConvertStringToBinary(originHighScoreString, BinaryStringEncoding.Utf8);
            return CryptographicBuffer.EncodeToBase64String(buffUTF8);
        }

        /// <summary>
        /// saves the highscoreString in the datafile
        /// </summary>
        private async void writeToDataFile()
        {
            await FileIO.WriteTextAsync(dataFile, getBase64HighScoreString());
        }

        /// <summary>
        /// refreshes the whole board
        /// used when the game is reset or the difficulty is changed
        /// </summary>
        private void refreshBoardView()
        {
            // must add 1 in Windows 10, otherwise one column will disappear
            boardView.Width = board.actualBoardSize[(int)board.currentBoardSize].y * 36 + 1;
            boardView.Height = board.actualBoardSize[(int)board.currentBoardSize].x * 36;
            boardView.ItemsSource = board.zoneList;
        }

        /// <summary>
        /// starts recording game elapsed time
        /// resets the displayed elapsed time
        /// </summary>
        private void gameStart()
        {
            startTime = DateTime.Now;
            timeElaspedTextBlock.Text = "0";
            gameElapsedTimer.Start();
        }

        /// <summary>
        /// game finished
        /// records game elapsed time
        /// save new highscore if neccessary
        /// </summary>
        private void gameOver()
        {
            elapsedTime = DateTime.Now.Subtract(startTime);
            gameElapsedTimer.Stop();
            int elapsedTimeInMillisecond = (int)elapsedTime.TotalMilliseconds;
            // new highscore
            if (highscores[(int)gameDifficulty] == -1 || elapsedTimeInMillisecond < highscores[(int)gameDifficulty])
            {
                messageBox(
                    "Congratulations! You achieved a new highscore of "
                    + convertMillisecondsToSecondsString(elapsedTimeInMillisecond)
                    + "s"
                );
                highscores[(int)gameDifficulty] = elapsedTimeInMillisecond;
                writeToDataFile();
            }
        }

        /// <summary>
        /// handle the event that a zone is clicked
        /// </summary>
        /// <param name="sender">the zone clicked by the user</param>
        /// <param name="e"></param>
        private void Zone_Click(object sender, RoutedEventArgs e)
        {
            // ####
            Button button = sender as Button;
            writeLog(button.Tag as string);
            if (gameStatus == Status.LocalWaiting && board.currentBoardState == Board.state.playing)
            {
                gameStart();
                gameStatus = Status.LocalPlaying;
            }
            if (gameStatus == Status.LocalPlaying || gameStatus == Status.NetPlaying)
            {
                // Button button = sender as Button;
                board.open(int.Parse(button.Tag as string));
            }
            if (gameStatus == Status.NetPlaying && finished == 0)
                sendBoardStatistic();
        }

        /// <summary>
        /// mark a bomb
        /// </summary>
        /// <param name="sender">the button right clicked by the user</param>
        /// <param name="e"></param>
        private void Zone_Right_Click(object sender, RoutedEventArgs e)
        {
            if (gameStatus == Status.LocalWaiting || gameStatus == Status.LocalPlaying || gameStatus == Status.NetPlaying)
            {
                Button button = sender as Button;
                board.mark(int.Parse(button.Tag as string));
            }
            if (gameStatus == Status.NetPlaying)
                sendBoardStatistic();
        }

        /// <summary>
        /// handles the event that a zone is double clicked
        /// </summary>
        /// <param name="sender">the zone double clicked by the user</param>
        /// <param name="e"></param>
        private void Zone_Double_Click(object sender, RoutedEventArgs e)
        {
            if (gameStatus == Status.LocalPlaying || gameStatus == Status.NetPlaying)
            {
                Button button = sender as Button;
                board.explore(int.Parse(button.Tag as string));
            }
            if (gameStatus == Status.NetPlaying && finished == 0)
                sendBoardStatistic();
        }

        /// <summary>
        /// start a new game
        /// </summary>
        /// <param name="sender">the replay button</param>
        /// <param name="e"></param>
        private void replay(object sender, RoutedEventArgs e)
        {
            if (gameStatus == Status.LocalWaiting || gameStatus == Status.LocalPlaying)
            {
                board.reset();
                refreshBoardView();
                gameStatus = Status.LocalWaiting;
                gameElapsedTimer.Stop();
            }
            else
            {
                writeLog("只有在单机模式下才可以replay，请先结束对战");
            }
        }

        /// <summary>
        /// try to change the size of the board
        /// </summary>
        /// <param name="value">the new size</param>
        /// <returns>succeed or not</returns>
        private bool changeSize(Board.size value)
        {
            if (gameStatus == Status.LocalWaiting || gameStatus == Status.LocalPlaying || gameStatus == Status.NetListening || gameStatus == Status.NetConnected)
            {
                if (gameStatus == Status.LocalPlaying)
                    gameElapsedTimer.Stop();
                board.setBoardSize(value);
                refreshBoardView();
                if (gameStatus.ToString().StartsWith("Local"))
                    gameStatus = Status.LocalWaiting;
                return true;
            }
            else
            {
                writeLog("对战过程中不可以设定大小");
                return false;
            }
        }

        /// <summary>
        /// hack
        /// forces the progressBar to refresh
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void refreshProgressBar(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "numRevealedSafe")
                // hack the weird bug when switching from bigger map to small map
                selfRevealedProgressBar.Value = board.numRevealedSafe;
        }

        /// <summary>
        /// changes the difficulty to low
        /// </summary>
        /// <param name="sender">the low buton</param>
        /// <param name="e"></param>
        private void low(object sender, RoutedEventArgs e)
        {
            if (gameDifficulty != Difficulty.Low && changeSize(Board.size.small))
            {
                gameDifficulty = Difficulty.Low;
                lowButtonFill.Fill = lowLightBrush;
                intermediateButtonFill.Fill = intermediateDarkBrush;
                highButtonFill.Fill = highDarkBrush;
            }
        }

        /// <summary>
        /// changes the difficulty to intermediate
        /// </summary>
        /// <param name="sender">the intermediate button</param>
        /// <param name="e"></param>
        private void intermediate(object sender, RoutedEventArgs e)
        {
            if (gameDifficulty != Difficulty.Intermediate && changeSize(Board.size.medium))
            {
                gameDifficulty = Difficulty.Intermediate;
                lowButtonFill.Fill = lowDarkBrush;
                intermediateButtonFill.Fill = intermediateLightBrush;
                highButtonFill.Fill = highDarkBrush;
            }
        }

        /// <summary>
        /// changes the difficulty to high
        /// </summary>
        /// <param name="sender">the high button</param>
        /// <param name="e"></param>
        private void high(object sender, RoutedEventArgs e)
        {
            if (gameDifficulty != Difficulty.High && changeSize(Board.size.big))
            {
                gameDifficulty = Difficulty.High;
                lowButtonFill.Fill = lowDarkBrush;
                intermediateButtonFill.Fill = intermediateDarkBrush;
                highButtonFill.Fill = highLightBrush;
            }
        }

        /// <summary>
        /// used by the board to nofity the mainPage that the game is done
        /// </summary>
        private void winHandler()
        {
            if (gameStatus == Status.LocalPlaying)
            {
                gameStatus = Status.LocalWaiting;
                gameOver();
                messageBox("You win in " + convertMillisecondsToSecondsString((int)elapsedTime.TotalMilliseconds) + "s!");
            }
            else
            {
                finished = 1;
                gameOver();
                sendBySocket("done " + ((int)elapsedTime.TotalMilliseconds).ToString());
            }
        }

        /// <summary>
        /// used by the board to nofity the mainPage that the game is failed
        /// </summary>
        private void loseHandler()
        {
            gameElapsedTimer.Stop();
            if (gameStatus == Status.LocalPlaying)
            {
                gameStatus = Status.LocalWaiting;
                messageBox("You lose!");
            }
            // lose when dueling
            else
            {
                elapsedTime = DateTime.Now.Subtract(startTime);
                finished = -1;
                sendBySocket("fail " + ((int)elapsedTime.TotalMilliseconds).ToString());
            }
        }

        /// <summary>
        /// a simple utility function to pop a messageBox
        /// user will have to confirm before continuing
        /// should be used for important message
        /// </summary>
        /// <param name="message">message to be shown</param>
        private async void messageBox(string message)
        {
            // may be call simultanous and throw an exception
            try
            {
                MessageDialog hint = new MessageDialog(message);
                await hint.ShowAsync();
            }
            catch
            {
                // do nothing
            }
        }

        /// <summary>
        /// a simple utility function to output log message
        /// message will be displayed in a box
        /// the difference from messageBox is that it's not offensive
        /// should be used for unimportant message
        /// </summary>
        /// <param name="message">message to be shown</param>
        private void writeLog(string message)
        {
            log.Text += message;
            log.Text += Environment.NewLine;

            // log.Content += message + Environment.NewLine;
            // log.UpdateLayout();
            // log.ScrollToVerticalOffset(log.ScrollableHeight);
        }

        /// <summary>
        /// handler of the start listen button
        /// </summary>
        /// <param name="sender">the start listen button</param>
        /// <param name="e"></param>
        private void startListen(object sender, RoutedEventArgs e)
        {
            if (gameStatus == Status.LocalWaiting || gameStatus == Status.LocalPlaying)
                _startListen();
        }

        /// <summary>
        /// 在服务端启动一个 socket 监听
        /// </summary>
        private async void _startListen()
        {
            // 实例化一个 socket 监听对象
            _listener = new StreamSocketListener();
            // 监听在接收到一个连接后所触发的事件
            _listener.ConnectionReceived += _listener_ConnectionReceived;

            try
            {
                // 在指定的端口上启动 socket 监听
                // await _listener.BindServiceNameAsync("2211");
                await _listener.BindEndpointAsync(new HostName("192.168.1.101"), "2211");

                writeLog("已经在本机的 2211 端口启动了 socket(tcp) 监听");
                gameStatus = Status.NetListening;
            }
            catch (Exception ex)
            {
                SocketErrorStatus errStatus = SocketError.GetStatus(ex.HResult);

                writeLog("errStatus: " + errStatus.ToString());
                writeLog(ex.ToString());
            }
        }

        /// <summary>
        /// socket 监听接收到一个连接后
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        async void _listener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            if (gameStatus == Status.NetListening)
            {
                // 实例化一个 DataReader，用于读取数据
                DataReader reader = new DataReader(args.Socket.InputStream);

                await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    writeLog("服务端收到了来自: " + args.Socket.Information.RemoteHostName.RawName + ":" + args.Socket.Information.RemotePort + " 的 socket 连接");
                });

                // 记住client
                _client = args.Socket;

                // set status
                gameStatus = Status.NetConnected;

                try
                {
                    while (true)
                    {
                        // 自定义协议（header|body）：前4个字节代表实际数据的长度，之后的实际数据为一个字符串数据

                        // 读取 header
                        uint sizeFieldCount = await reader.LoadAsync(sizeof(uint));
                        if (sizeFieldCount != sizeof(uint))
                        {
                            // 在获取到合法数据之前，socket 关闭了
                            return;
                        }

                        // 读取 body
                        uint stringLength = reader.ReadUInt32();
                        uint actualStringLength = await reader.LoadAsync(stringLength);
                        if (stringLength != actualStringLength)
                        {
                            // 在获取到合法数据之前，socket 关闭了
                            return;
                        }

                        await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () =>
                        {
                            // 显示客户端发送过来的数据
                            string receivedData = reader.ReadString(actualStringLength);
                            // writeLog("接收到数据: " + receivedData);
                            messageHandler(receivedData);
                        });
                    }
                }
                catch (Exception ex)
                {
                    var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        SocketErrorStatus errStatus = SocketError.GetStatus(ex.HResult);

                        writeLog("errStatus: " + errStatus.ToString());
                        writeLog(ex.ToString());
                    });
                }
            }
        }

        /// <summary>
        /// process the data received from network
        /// take appropriate action accroding to the message and status
        /// </summary>
        /// <param name="receivedData"></param>
        private void messageHandler(string receivedData)
        {
            if (receivedData == "Bye")
            {
                writeLog("对手主动结束，游戏结束");
                netEndGame(this, new RoutedEventArgs());
                return;
            }
            // 收到准备信息
            else if (receivedData == "Ready" || receivedData.Length >= 81)
            {
                if (gameStatus == Status.NetConnected)
                    gameStatus = Status.NetReady;
                else
                    startCountDown();
                if (_listener == null || _listener.Information.LocalPort != "2211")
                {
                    board.FromString(receivedData);
                    refreshBoardView();
                    writeLog("已经收到地图,游戏可以开始...");
                }
            }
            // 游戏结束，对手结束了或者对面知道了
            else
            {
                string[] result = receivedData.Split(new char[] { ' ' });
                int ms = int.Parse(result[1]);
                TimeSpan ComponentTime = new TimeSpan(0, 0, ms / 1000 / 60, ms / 1000 % 60, ms % 1000);
                // 对手完成了
                if (result[0] == "done")
                {
                    writeLog("对手完成了，用时 " + ComponentTime.ToString());
                    // 这边更快
                    if (finished == 1 && elapsedTime.CompareTo(ComponentTime) < 0)
                    {
                        messageBox("You win!");
                    }
                    else
                    {
                        messageBox("You lose!");
                    }
                }
                // 对手扑街了
                else if (result[0] == "fail")
                {
                    writeLog("对手扑街了，用时 " + ComponentTime.ToString());
                    // 这边更快
                    if (finished == -1 && elapsedTime.CompareTo(ComponentTime) < 0)
                    {
                        messageBox("You lose!");
                    }
                    else
                    {
                        messageBox("You win!");
                    }
                }
                // 对手确认
                else if (result[0] == "gotit")
                {
                    if (finished == -1)
                    {
                        messageBox("You lose!");
                    }
                    else
                    {
                        messageBox("You win!");
                    }
                }
                // 对手状态更新
                else
                {
                    componentRevealedProgressBar.Value = int.Parse(result[0]);
                    componentMarkedProgressBar.Value = int.Parse(result[1]);
                }
                // 游戏结束
                if (result[0] == "done" || result[0] == "fail" || result[0] == "gotit")
                {
                    if (finished == 0)
                        sendBySocket("gotit 0");
                    gameElapsedTimer.Stop();
                    gameStatus = Status.NetConnected;
                }
            }
        }

        /// <summary>
        /// 开始游戏前的倒计时
        /// </summary>
        private void startCountDown()
        {
            finished = 0;
            gameStatus = Status.NetStarting;
            _countDown = 3;
            countDownTextBlock.Text = _countDown.ToString();
            countDownTextBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
            countdownTimer.Start();
        }

        /// <summary>
        /// handler for a tick of the countdownTimer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void countdownTick(object sender, object e)
        {
            writeLog("tick");
            --_countDown;
            if (_countDown == 0)
            {
                countdownTimer.Stop();
                countDownTextBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                if (gameStatus == Status.NetStarting)
                {
                    gameStatus = Status.NetPlaying;
                    gameStart();
                }
            }
            else
            {
                countDownTextBlock.Text = _countDown.ToString();
            }
        }

        /// <summary>
        /// handler for tick of the gameElapsedTimer
        /// updates the timeElaspedTextBlock
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void gameElapsedTick(object sender, object e)
        {
            TimeSpan timeElapsedByNow = DateTime.Now.Subtract(startTime);
            timeElaspedTextBlock.Text = ((int)timeElapsedByNow.TotalSeconds).ToString();
        }

        /// <summary>
        /// 创建一个客户端 socket，并连接到服务端 socket
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void connectToServer(object sender, RoutedEventArgs e)
        {
            if (gameStatus == Status.LocalWaiting || gameStatus == Status.LocalPlaying)
            {
                // debug
                writeLog("connecting to " + serverIp.Text);
                HostName hostName;
                try
                {
                    hostName = new HostName(serverIp.Text);
                    // hostName = new HostName("127.0.0.1");
                }
                catch (Exception ex)
                {
                    writeLog(ex.ToString());
                    return;
                }

                // 实例化一个客户端 socket 对象
                _client = new StreamSocket();

                try
                {
                    // 连接到指定的服务端 socket
                    await _client.ConnectAsync(hostName, "2211");

                    gameStatus = Status.NetConnected;

                    writeLog("已经连接上了 " + serverIp.Text + ":2211");
                    // 实例化一个 DataReader，用于读取数据
                    DataReader reader = new DataReader(_client.InputStream);

                    try
                    {
                        while (true)
                        {
                            // 自定义协议（header|body）：前4个字节代表实际数据的长度，之后的实际数据为一个字符串数据

                            // 读取 header
                            uint sizeFieldCount = await reader.LoadAsync(sizeof(uint));
                            if (sizeFieldCount != sizeof(uint))
                            {
                                // 在获取到合法数据之前，socket 关闭了
                                return;
                            }

                            // 读取 body
                            uint stringLength = reader.ReadUInt32();
                            uint actualStringLength = await reader.LoadAsync(stringLength);
                            if (stringLength != actualStringLength)
                            {
                                // 在获取到合法数据之前，socket 关闭了
                                return;
                            }

                            await this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                            () =>
                            {
                                // 显示客户端发送过来的数据
                                string receivedData = reader.ReadString(actualStringLength);
                                // writeLog("接收到数据: " + receivedData);
                                messageHandler(receivedData);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        var ignore = this.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            SocketErrorStatus errStatus = SocketError.GetStatus(ex.HResult);

                            writeLog("errStatus: " + errStatus.ToString());
                            writeLog(ex.ToString());
                        });
                    }
                    //writeLog("已经连接上了 127.0.0.1:2211");
                }
                catch (Exception ex)
                {
                    SocketErrorStatus errStatus = SocketError.GetStatus(ex.HResult);

                    writeLog("errStatus: " + errStatus.ToString());
                    writeLog(ex.ToString());
                }
            }
        }

        /// <summary>
        /// handler of the start game button
        /// sends out the map if this is server
        /// sends out ready if this is client
        /// starts the game if both player is ready
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void netStartGame(object sender, RoutedEventArgs e)
        {
            if (gameStatus == Status.NetConnected || gameStatus == Status.NetReady)
            {
                // 服务器发地图
                if (_listener != null && _listener.Information.LocalPort == "2211")
                {
                    board.reset();
                    refreshBoardView();
                    writeLog("正在向客户端发送地图...");
                    sendBySocket(board.ToString(), "发送成功，即将开始游戏");
                }
                // 客户端发Ready
                else
                {
                    writeLog("正在发出准备信息...");
                    sendBySocket("Ready", "成功发送准备信息");
                }
                if (gameStatus == Status.NetConnected)
                    gameStatus = Status.NetReady;
                else
                    startCountDown();
            }
        }

        /// <summary>
        /// 发送已翻开的数量和已插旗的数量
        /// 用于board更新时将新状态发送给对方
        /// 信息将被用于更新进度条
        /// 特别地，游戏完成时不会被调用
        /// </summary>
        private void sendBoardStatistic()
        {
            sendBySocket(board.numRevealedSafe.ToString() + " " + board.numMarkedMines.ToString());
        }

        /// <summary>
        /// handler of the disconnect button
        /// closes the socket
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void netEndGame(object sender, RoutedEventArgs e)
        {
            if (gameStatus.ToString().StartsWith("Net"))
            {
                gameStatus = Status.LocalWaiting;
                if (_client != null)
                    sendBySocket("Bye");
                closeSocket();
            }
        }

        /// <summary>
        /// 从客户端 socket 发送一个字符串数据到服务端 socket
        /// </summary>
        /// <param name="message"></param>
        /// <param name="successEcho"></param>
        private async void sendBySocket(string message, string successEcho = null)
        {
            // 实例化一个 DataWriter，用于发送数据
            if (_writer == null)
                _writer = new DataWriter(_client.OutputStream);

            // 自定义协议（header|body）：前4个字节代表实际数据的长度，之后的实际数据为一个字符串数据

            _writer.WriteUInt32(_writer.MeasureString(message)); // 写 header 数据
            _writer.WriteString(message); // 写 body 数据

            try
            {
                // 发送数据
                await _writer.StoreAsync();

                if (successEcho != null)
                    writeLog(successEcho);
            }
            catch (Exception ex)
            {
                SocketErrorStatus errStatus = SocketError.GetStatus(ex.HResult);

                writeLog("errStatus: " + errStatus.ToString());
                writeLog(ex.ToString());
            }
        }

        // 关闭客户端 socket 和服务端 socket
        private void closeSocket()
        {
            try
            {
                if (_writer != null)
                {
                    _writer.DetachStream(); // 分离 DataWriter 与 Stream 的关联
                    _writer.Dispose();
                    _writer = null;
                }

                if (_client != null)
                {
                    _client.Dispose();
                    _client = null;
                }
                if (_listener != null)
                {
                    _listener.Dispose();
                    _listener = null;
                }

                writeLog("已经断开连接");
                gameStatus = Status.LocalWaiting;
            }
            catch (Exception ex)
            {
                writeLog(ex.ToString());
            }
        }

        /// <summary>
        /// handler of the help button
        /// displays help message
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showHelp(object sender, RoutedEventArgs e)
        {
            // todo
            messageBox("左键翻开，右键标雷，双击自动打开。\n"
                + "网络对战时，由其中一方启动服务器，"
                + "另一方输入服务器端的IP地址并连接，"
                + "双方都点击\"Duel\"按钮后游戏即会开始。"
                + "需要结束时点击\"结束对战\"按钮即可与其他玩家再次连接。");
        }

        /// <summary>
        /// handler of the share button
        /// allows the user to share content with his friends
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void share(object sender, RoutedEventArgs e)
        {
            DataTransferManager.ShowShareUI();
        }

        /// <summary>
        /// handler of the showHighScore button
        /// displays the local highscore
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void showHighScore(object sender, RoutedEventArgs e)
        {
            string highScoreString = "";
            for (int i = (int)Difficulty.Low; i <= (int)Difficulty.High; ++i)
            {
                highScoreString += difficultyString[i] + ": ";
                if (highscores[i] == -1)
                {
                    highScoreString += "无";
                }
                else
                {
                    highScoreString += convertMillisecondsToSecondsString(highscores[i]) + "秒";
                }
                highScoreString += "\n";
            }
            messageBox(highScoreString);
        }

        /// <summary>
        /// converts number of milliseconds to a string like "*.*s"
        /// </summary>
        /// <param name="milliseconds"></param>
        /// <returns></returns>
        static private string convertMillisecondsToSecondsString(int milliseconds)
        {
            return (milliseconds / 1000).ToString() + "." + (milliseconds % 1000).ToString();
        }

        /// <summary>
        /// accomplishes for share functionality
        /// collects the content to be shared
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainPage_DataRequested(DataTransferManager sender, DataRequestedEventArgs e)
        {
            DataRequest request = e.Request;

            // Because we are making async calls in the DataRequested event handler,
            // we need to get the deferral first.
            DataRequestDeferral deferral = request.GetDeferral();

            // Make sure we always call Complete on the deferral.
            request.Data.Properties.Title = "最高分";
            request.Data.Properties.Description = "分享战绩";
            string textToShare = "我在扫雷中获得战绩:";
            for (int i = (int)Difficulty.Low; i <= (int)Difficulty.High; ++i)
            {
                textToShare += " ";
                textToShare += difficultyString[i] + ": ";
                if (highscores[i] == -1)
                {
                    textToShare += "无";
                }
                else
                {
                    textToShare += convertMillisecondsToSecondsString(highscores[i]) + "秒";
                }
            }
            request.Data.SetText(textToShare);
            deferral.Complete();
        }
    }
}
