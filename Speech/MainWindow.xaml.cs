using Microsoft.Win32;
using System;
using System.Threading;
using System.Speech.Recognition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Speech
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            Init();
        }

        /// <summary>
        /// オーディオ再生プレイヤー
        /// </summary>
        private MediaPlayer _player = null;

        /// <summary>
        /// オーディオ時間表示用タイマー
        /// </summary>
        private DispatcherTimer _timer = null;

        /// <summary>
        /// 音声解析エンジン(ファイル用)
        /// </summary>
        private SpeechRecognitionEngine _fileEngine = null;
        
        /// <summary>
        /// 音声解析エンジン(マイク用)
        /// </summary>
        private SpeechRecognitionEngine _micEngine = null;

        /// <summary>
        /// 一時的に音声の一部を認識した場合のイベントを設定、取得します。
        /// </summary>
        private Action<SpeechHypothesizedEventArgs> _speechHypothesizedEvent = null;

        /// <summary>
        /// 信頼性の高い 1 つ以上の句を認識した場合のイベントを設定、取得します。
        /// </summary>
        private Action<SpeechRecognizedEventArgs> _speechRecognizedEvent = null;

        /// <summary>
        /// 信頼性の低い候補句のみ認識した場合のイベントを設定、取得します。
        /// </summary>
        private Action<SpeechRecognitionRejectedEventArgs> _speechRecognitionRejectedEvent = null;

        /// <summary>
        /// 音声認識が終了した場合のイベントを設定、取得します。
        /// </summary>
        private Action<RecognizeCompletedEventArgs> _speechRecognizeCompletedEvent = null;

        /// <summary>
        /// 音声再生時間
        /// </summary>
        public double PlayTimeSliderValue
        {
            get { return _playTimeSliderValue; }
            set { _playTimeSliderValue = value; }
        }
        private double _playTimeSliderValue;

        /// <summary>
        /// 初期化
        /// </summary>
        private void Init()
        {
            PlayTimeSliderValue = 0;

            _speechHypothesizedEvent = (e) =>
            {
                //
            };

            _speechRecognizedEvent = (e) =>
            {
                //
            };

            _speechRecognitionRejectedEvent = (e) =>
            {
                //
            };

            _speechRecognizeCompletedEvent = (e) =>
            {
                Progress.IsIndeterminate = false;
                Progress.Value = 1;
                StatusText.Text = "解析完了";
                RecordText.Text += e.Result.Text + "\r\n";
                RecordText.Text += " ----- 認識終了 -----\r\n";
            };
        }

        /// <summary>
        /// 解析開始
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartAnalyze(object sender, RoutedEventArgs e)
        {
            if (MainTab.SelectedIndex == 0)
            {
                StartAnalyzeFile();
            }
            else
            {
                StartAnalyzeMic();
            }
        }

        /// <summary>
        /// 解析開始(ファイル)
        /// </summary>
        private void StartAnalyzeFile()
        {
            if (string.IsNullOrEmpty(SrcFileBox.Text))
            {
                MessageBox.Show("入力ファイルが選択されていません。", "解析エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Progress.IsIndeterminate = true;
            RecordText.Text = "";
            StatusText.Text = "解析中...";

            _fileEngine = null;
            _fileEngine = new SpeechRecognitionEngine();
            _fileEngine.RecognizeCompleted += SpeechRecognizeCompleted;
            _fileEngine.SetInputToWaveFile(SrcFileBox.Text);
            _fileEngine.LoadGrammar(new DictationGrammar());
            _fileEngine.RecognizeAsync();
        }

        /// <summary>
        /// 解析開始(マイク)
        /// </summary>
        private void StartAnalyzeMic()
        {
            _micEngine = new SpeechRecognitionEngine();
            _micEngine.RecognizeCompleted += SpeechRecognizeCompleted;
            _micEngine.LoadGrammar(new DictationGrammar() { Name = "Dictation" });
            _micEngine.SetInputToDefaultAudioDevice();

            if (String.Equals(StartStopButton.Content, "解析開始"))
            {
                Progress.IsIndeterminate = true;
                RecordText.Text = "";
                StatusText.Text = "解析中...";
                StartStopButton.Content = "解析終了";

                _micEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
            else
            {
                Progress.IsIndeterminate = false;
                StatusText.Text = "";
                StartStopButton.Content = "解析開始";

                _micEngine.RecognizeAsyncStop();
            }
        }

        /// <summary>
        /// 参照ボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RefButtonClick(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog()
            {
                FilterIndex = 1,
                Filter = "Waveサウンドファイル(*.wave;*.wav)|*.wave;*.wav",
                Title = "Waveサウンドファイルを選択してください",
                RestoreDirectory = true
            };

            if ((bool)!ofd.ShowDialog())
            {
                return;
            }

            _player = null;
            _player = new MediaPlayer();
            _player.Open(new Uri(ofd.FileName, UriKind.Absolute));
            _player.MediaEnded += new EventHandler((es, ee) =>
            {
                StopButtonClick(es, null);
            });
            while (true)
            {
                if (_player.NaturalDuration.HasTimeSpan)
                {
                    break;
                }
                Thread.Sleep(0);
            }
            DisplayTime.Text = "00:00:00";
            DurationTime.Text = String.Format("{0:d2}:{1:d2}:{2:d2}",
                _player.NaturalDuration.TimeSpan.Hours, _player.NaturalDuration.TimeSpan.Minutes, _player.NaturalDuration.TimeSpan.Seconds);

            StatusText.Text = "解析準備完了";
            SrcFileBox.Text = ofd.FileName;
            Progress.Value = 0;
        }

        /// <summary>
        /// 再生ボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PlayButtonClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SrcFileBox.Text))
            {
                return;
            }

            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
            _timer = new DispatcherTimer(DispatcherPriority.Normal);
            _timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            _timer.Tick += new EventHandler((ts, te) =>
            {
                if (_player == null)
                {
                    return;
                }
                DisplayTime.Text = String.Format("{0:d2}:{1:d2}:{2:d2}",
                    _player.Position.Hours, _player.Position.Minutes, _player.Position.Seconds);
                var c = Math.Ceiling(_player.Position.TotalMilliseconds);
                var t = Math.Ceiling(_player.NaturalDuration.TimeSpan.TotalMilliseconds);
                PlayTimeSlider.Value = Math.Ceiling(c / t * 1000);
            });
            _timer.Start();
            _player.Play();
        }

        /// <summary>
        /// 音声停止ボタンクリック
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StopButtonClick(object sender, RoutedEventArgs e)
        {
            if (_player == null)
            {
                return;
            }
            if (_timer != null)
            {
                _timer.Stop();
            }
            _player.Stop();
            _player.Close();
            _player.Open(new Uri(SrcFileBox.Text, UriKind.Absolute));
            DisplayTime.Text = "00:00:00";
            PlayTimeSlider.Value = 0;
        }

        /// <summary>
        /// 一時的に音声の一部を認識した場合のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            if (e.Result != null && _speechHypothesizedEvent != null)
            {
                _speechHypothesizedEvent(e);
            }
        }

        /// <summary>
        /// 信頼性の高い 1 つ以上の句を認識した場合のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result != null && _speechRecognizedEvent != null)
            {
                _speechRecognizedEvent(e);
            }
        }

        /// <summary>
        /// 信頼性の低い候補句のみ認識した場合のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            if (e.Result != null && _speechRecognitionRejectedEvent != null)
            {
                _speechRecognitionRejectedEvent(e);
            }
        }

        /// <summary>
        /// 音声認識が終了した場合のイベント
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SpeechRecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
        {
            if (e.Result != null && _speechRecognizeCompletedEvent != null)
            {
                _speechRecognizeCompletedEvent(e);
            }
        }
    }
}
