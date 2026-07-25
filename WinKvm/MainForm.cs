using DirectShowLib;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Linq;

namespace WinKvm
{
    public partial class MainForm : Form
    {
        // VideoCapture Worker
        Mat? _frame = null;
        VideoCapture? _capture = null;

        // シリアルポート
        string _serialPortName = "";
        SerialPort? _serialPort = null;

        // 画面サイズ指定
        double _scale = 1.0;
        DsDevice? _device;
        Resolution? _resolution;
        readonly ManualResetEvent _resetVideoEvent = new(false);
        readonly CancellationTokenSource _videoCts = new();

        // logger
        readonly ILoggerFactory? _loggerFactory;
        readonly ILogger<MainForm>? _logger;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainForm(ILoggerFactory loggerFactory)
        {
            try
            {
                _loggerFactory = loggerFactory;
                _logger = _loggerFactory.CreateLogger<MainForm>();
                InitializeComponent();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in MainForm()");
                throw;
            }
            finally
            {
                _logger?.LogTrace("Leave: MainForm()");
            }
        }

        /// <summary>
        /// 初期化
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");

                // シリアル設定
                _serialPortName = Properties.Settings.Default.SerialPort;
                if (_serialPortName != "")
                {
                    try
                    {
                        _serialPort = new(_serialPortName, 9600, Parity.None, 8, StopBits.One);
                        _serialPort.Open();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in serial port open");
                    }
                }
                if (_serialPort == null)
                {
                    // シリアルポートが設定されていない
                    _serialPort = null;
                    _serialPortName = "";
                    statusLabelSerial.Text = "【シリアルポートを設定してください】";
                }
                else
                {
                    statusLabelSerial.Text = $"【{_serialPortName}】";
                }
                foreach (var portName in SerialPort.GetPortNames())
                {
                    // シリアルポートメニュー設定
                    var item = new ToolStripMenuItem(portName);
                    item.Click += SerialPortMenu_Click;
                    if (_serialPortName == portName)
                        item.Checked = true;
                    menuItemKeyMouse.DropDownItems.Add(item);
                    _logger?.LogDebug("Serial port menu added: {portName}, {checked}", portName, item.Checked);
                }

                // ビデオメニュー設定

                // スケール
                _scale = Properties.Settings.Default.VideoScale;
                foreach (var scale in new int[] { 50, 75, 100, 125, 150, 175, 200 })
                {
                    var item = new ToolStripMenuItem
                    {
                        Text = $"{scale}%",
                        Tag = scale,
                    };
                    item.Click += ScaleMenu_Click;
                    if (_scale == scale / 100.0)
                        item.Checked = true;
                    menuItemVideo.DropDownItems.Add(item);
                    _logger?.LogDebug("Scale menu added: {scale}, {checked}", _scale, item.Checked);
                }
                menuItemVideo.DropDownItems.Add(new ToolStripSeparator());

                // デバイス名
                foreach (var capDevice in DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice))
                {
                    // デバイス名メニュー設定
                    ToolStripMenuItem deviceMenu = new(capDevice.Name)
                    {
                        Tag = capDevice
                    };
                    foreach (var resolution in DsUtil.GetAllAvailableResolution(capDevice).ToList()
                        .OrderBy(m => m.FourCc).ThenByDescending(m => m.Width).ThenByDescending(m => m.Height))
                    {
                        // 解像度メニュー設定
                        var name = $"{resolution.Width}x{resolution.Height}[{resolution.Fps}] ({resolution.FourCc})";
                        _logger?.LogInformation("Add menu item: {device}, {Name}", capDevice.Name, name);

                        ToolStripMenuItem resolutionMenu = new(name)
                        {
                            Tag = resolution,
                        };
                        resolutionMenu.Click += ResolutionMenu_Click;

                        // 設定ファイルの解像度と一致する場合は初期値として選択
                        if (capDevice.Name == Properties.Settings.Default.VideoName &&
                            resolution.FourCc == Properties.Settings.Default.VideoFourCC &&
                            resolution.Width == Properties.Settings.Default.VideoWidth &&
                            resolution.Height == Properties.Settings.Default.VideoHeight)
                        {
                            _logger?.LogInformation("Set initial selection: {device}, {Name}", capDevice.Name, name);
                            _device = capDevice;
                            _resolution = resolution;
                            resolutionMenu.Checked = true;
                        }
                        // 解像度メニュー追加
                        deviceMenu.DropDownItems.Add(resolutionMenu);
                    }

                    // デバイスメニュー追加
                    menuItemVideo.DropDownItems.Add(deviceMenu);
                }

                // マウスイベント
                pictureBox.MouseWheel += PictureBox_MouseWheel;

                _ = Task.Run(() => VideoCaptureWorker());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                throw;
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// シリアルポート選択
        /// </summary>
        private void SerialPortMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");

                if (sender is not ToolStripMenuItem senderMenuItem)
                {
                    _logger?.LogWarning("Invalid sender: {sender}", sender);
                    return;
                }

                // 同じシリアルポートの場合は何もしない
                if (senderMenuItem.Text == _serialPortName)
                {
                    _logger?.LogDebug("Same serial port: {portName}", _serialPortName);
                    return;
                }

                // シリアルポート変更
                _serialPortName = senderMenuItem.Text;
                SaveSerialSettings();
                try
                {
                    _logger?.LogInformation("Change serial port start: {portName}", _serialPortName);
                    _serialPort?.Close();
                    _serialPort = new(_serialPortName, 9600, Parity.None, 8, StopBits.One);
                    _serialPort.Open();
                    _logger?.LogInformation("Change serial port success: {portName}", _serialPortName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in serial port open: {erialPortName}", _serialPortName);
                    _serialPort = null;
                }
                if (_serialPort == null)
                {
                    // シリアルポートが設定されていない
                    _serialPortName = "";
                    statusLabelSerial.Text = "【シリアルポートを設定してください】";
                }
                else
                {
                    statusLabelSerial.Text = $"【{_serialPortName}】";
                }

                // チェックマーク設定
                foreach (ToolStripMenuItem menuItem in menuItemKeyMouse.DropDownItems)
                {
                    if (menuItem.Text == _serialPortName)
                        menuItem.Checked = true;
                    else
                        menuItem.Checked = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                throw;
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// スケール変更
        /// </summary>
        private void ScaleMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");

                if (sender is not ToolStripMenuItem menuItem)
                {
                    _logger?.LogError("Error in {MethodName}: sender is not ToolStripMenuItem", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                    return;
                }
                if (menuItem.Tag is not int scale)
                {
                    _logger?.LogError("Error in {MethodName}: menuItem.Tag is not int", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                    return;
                }
                _scale = scale / 100.0;
                _logger?.LogInformation("Set scale to {scale}", _scale);
                SaveVideoSettings();

                // メニューのチェックを更新
                foreach (var item in menuItemVideo.DropDownItems)
                {
                    if (item is not ToolStripMenuItem m) continue;
                    if (m.Tag is int sc)
                        m.Checked = sc == scale;
                }

                // 画面サイズ変更
                Invoke(() =>
                {
                    if (_resolution != null)
                    {
                        this.pictureBox.Width = (int)(_resolution.Width * _scale);
                        this.pictureBox.Height = (int)(_resolution.Height * _scale);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                throw;
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// ビデオ選択変更
        /// </summary>
        private void ResolutionMenu_Click(object? sender, EventArgs e)
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");

                // 解像度メニュー選択
                if (sender is not ToolStripMenuItem menuItem)
                {
                    _logger?.LogError("Error in {MethodName}: sender is not ToolStripMenuItem", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                    return;
                }
                if (menuItem.Tag is not Resolution resolution)
                {
                    _logger?.LogError("Error in {MethodName}: menuItem.Tag is not Resolution", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                    return;
                }
                var deviceMenuItem = menuItem.OwnerItem;
                if (deviceMenuItem == null)
                {
                    _logger?.LogError("Error in {MethodName}: deviceMenuItem is null", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                    return;
                }
                if (deviceMenuItem.Tag is not DsDevice capDevice)
                {
                    _logger?.LogError("Error in {MethodName}: deviceMenuItem.Tag is not DsDevice", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                    return;
                }
                if (_device == null || _device.Name != capDevice.Name || _resolution == null ||
                    _resolution.FourCc != resolution.FourCc || _resolution.Width != resolution.Width || _resolution.Height != resolution.Height)
                {
                    // デバイスもしくは解像度が変更された
                    _device = capDevice;
                    _resolution = resolution;
                    SaveVideoSettings();
                    foreach (var menu1 in menuItemVideo.DropDownItems)
                    {
                        if (menu1 is not ToolStripMenuItem deviceMenu) continue;
                        if (deviceMenu.Tag is not DsDevice) continue;
                        foreach (ToolStripMenuItem resolutionMenu in deviceMenu.DropDownItems)
                        {
                            if (resolutionMenu.Tag is not Resolution) continue;
                            resolutionMenu.Checked = false;
                        }
                    }
                    menuItem.Checked = true;
                    _resetVideoEvent.Set();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// ビデオ表示
        /// </summary>
        private void VideoCaptureWorker()
        {
            try
            {
                _logger?.LogTrace("Enter: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                _frame = new();
                _capture = new();
                while (!_videoCts.IsCancellationRequested)
                {
                    try
                    {
                        // デバイスもしくは解像度が設定されるまで待機
                        if (_device == null || _resolution == null)
                        {
                            _logger?.LogInformation("Wait for video settings to be enabled.");
                            Invoke(() =>
                            {
                                statusLabelVideo.Text = "ビデオデバイスを選択してください。";
                            });
                            WaitHandle.WaitAny(new WaitHandle[] { _resetVideoEvent, _videoCts.Token.WaitHandle });
                            if (_videoCts.IsCancellationRequested) return;
                        }
                        if (_device == null || _resolution == null) continue;

                        // デバイスID取得
                        var deviceId = -1;
                        var index = 0;
                        foreach (var item in menuItemVideo.DropDownItems)
                        {
                            if (item is not ToolStripMenuItem menuItem) continue;
                            if (menuItem.Tag is not DsDevice dsDevice) continue;
                            if (dsDevice.Name == _device.Name)
                            {
                                deviceId = index;
                                _logger?.LogInformation("deviceId found: {deviceId}", deviceId);
                                break;
                            }
                            index++;
                        }
                        if (deviceId == -1)
                        {
                            _logger?.LogInformation("deviceId not found");
                            _device = null;
                            _resolution = null;
                        }

                        //カメラの起動
                        Invoke(() =>
                        {
                            this.pictureBox.Image = null;
                            this.pictureBox.Width = (int)(_resolution.Width * _scale);
                            this.pictureBox.Height = (int)(_resolution.Height * _scale);
                            statusLabelVideo.Text = $"{_device?.Name ?? ""}: {_resolution.Width}x{_resolution.Height}[{_resolution.Fps}] ({_resolution.FourCc})"; ;
                        });
                        _logger?.LogInformation("Open device id({deviceId}), W={width}, H={height}, FPS={fps}, FourCC={fourCc}.",
                            deviceId, _resolution.Width, _resolution.Height, _resolution.Fps, _resolution.FourCc);
                        _capture.Open(deviceId, VideoCaptureAPIs.DSHOW);
                        _capture.FrameWidth = _resolution.Width;
                        _capture.FrameHeight = _resolution.Height;
                        _capture.Fps = _resolution.Fps;
                        _capture.FourCC = _resolution.FourCc;
                        if (!_capture.IsOpened())
                        {
                            MessageBox.Show("カメラの起動に失敗しました。", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            _device = null;
                            _resolution = null;
                            _logger?.LogError("Open device failed");
                            continue;
                        }
                        _logger?.LogInformation("Open success.");

                        while (!_videoCts.IsCancellationRequested && !_resetVideoEvent.WaitOne(0))
                        {
                            // デバイスや解像度を変更しない限り、このループを抜けない
                            try
                            {
                                _capture.Read(_frame);
                                if (_frame.Empty() || _frame.Size().Width <= 0 || _frame.Size().Height <= 0) continue;
                                Cv2.Resize(
                                    _frame[new Rect(Properties.Settings.Default.TrimLeft,
                                            Properties.Settings.Default.TrimTop,
                                            _frame.Width - Properties.Settings.Default.TrimLeft - Properties.Settings.Default.TrimRight,
                                            _frame.Height - Properties.Settings.Default.TrimTop - Properties.Settings.Default.TrimBottom)],
                                    _frame,
                                    new OpenCvSharp.Size((int)(_frame.Size().Width * _scale), (int)(_frame.Size().Height * _scale)));
                                //PictureBoxに表示　MatをBitMapに変換
                                var bitmap = BitmapConverter.ToBitmap(_frame);
                                try
                                {
                                    Invoke(() =>
                                    {
                                        pictureBox.Image?.Dispose();
                                        pictureBox.Image = bitmap;
                                    });
                                }
                                catch { }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error in video capture loop in {MethodName}, continue.", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                            }
                        }

                        // キャンセルチェック
                        if (_videoCts.IsCancellationRequested)
                            break;// throw new OperationCanceledException();

                        // 解像度変更
                        _logger?.LogInformation("Restart video device.");
                        _capture.Release();
                        _resetVideoEvent.Reset();
                    }
                    catch (OperationCanceledException)
                    {
                        _logger?.LogInformation("VideoCaptureWorker exit, by operation canceled.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in {MethodName}, continue.", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
                    }
                }   // while
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in {MethodName}, exit.", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
            finally
            {
                _logger?.LogTrace("Leave: {MethodName}", System.Reflection.MethodBase.GetCurrentMethod()?.Name ?? "");
            }
        }

        /// <summary>
        /// ビデオ設定を保存
        /// </summary>
        private void SaveVideoSettings()
        {
            Properties.Settings.Default.VideoName = _device?.Name;
            Properties.Settings.Default.VideoFourCC = _resolution?.FourCc;
            Properties.Settings.Default.VideoWidth = _resolution?.Width ?? 0;
            Properties.Settings.Default.VideoHeight = _resolution?.Height ?? 0;
            Properties.Settings.Default.VideoScale = _scale;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// シリアルポート設定を保存
        /// </summary>
        private void SaveSerialSettings()
        {
            Properties.Settings.Default.SerialPort = _serialPortName;
            Properties.Settings.Default.Save();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _videoCts.Cancel();
            _capture?.Release();
            _capture?.Dispose();
            _frame?.Dispose();
        }

        private void PictureBox_MouseEnter(object sender, EventArgs e)
        {
            Debug.WriteLine($"MouseEnter");
            Cursor.Hide();
        }

        private void PictureBox_MouseLeave(object sender, EventArgs e)
        {
            Debug.WriteLine($"MouseLeave");
            Cursor.Show();
        }

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            Debug.WriteLine($"MouseDown: {e.Button}, {e.Location}");
            MoveMouseRelative(0, 0, 0, e.Button);
        }

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            Debug.WriteLine($"MouseMove: {e.Button}, {e.Location}");
            MoveMouseAbsolute(e.X, e.Y, e.Button);
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            Debug.WriteLine($"MouseUp: {e.Button}, {e.Location}");
            MoveMouseRelative(0, 0, 0, MouseButtons.None);
        }

        private void PictureBox_MouseWheel(object? sender, MouseEventArgs e)
        {
            Debug.WriteLine($"MouseWheel: {e.Delta}");
            MoveMouseRelative(0, 0, e.Delta, MouseButtons.None);
        }

        /// <summary>
        /// マウスを移動（絶対位置）
        /// </summary>
        private void MoveMouseAbsolute(int x, int y, MouseButtons button)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            if(_resolution == null) return;
            var xc = (int)(4096 * x / (_resolution.Width * _scale));
            var yc = (int)(4096 * y / (_resolution.Height * _scale));
            var btn = (byte)((button == MouseButtons.Left ? 0x01 : 0x00) |
                (button == MouseButtons.Right ? 0x02 : 0x00) |
                (button == MouseButtons.Middle ? 0x04 : 0x00));

            var cmd = new byte[] { 0x57, 0xab, 0x00, 0x04, 0x07, 0x02, btn, (byte)(xc & 0xff), (byte)(xc >> 8), (byte)(yc & 0xff), (byte)(yc >> 8), 0x00, 0x00 };
            cmd[cmd.Length - 1] = (byte)(cmd.Sum(x => x) & 0xff);
            _serialPort.Write(cmd, 0, cmd.Length);
            var buf = new byte[7];
            _serialPort.Read(buf, 0, buf.Length);
        }

        /// <summary>
        /// マウスを移動（相対位置）
        /// </summary>
        private void MoveMouseRelative(int x, int y, int delta, MouseButtons button)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            var xc = x < 0 ? (byte)((x + 0x100) * _scale) : (byte)(x * _scale);
            var yc = y < 0 ? (byte)((y + 0x100) * _scale) : (byte)(y * _scale);
            var deltac = delta < 0 ? (byte)(delta / 120 + 0x100) : (byte)(delta / 120);
            var btn = (byte)((button == MouseButtons.Left ? 0x01 : 0x00) |
                (button == MouseButtons.Right ? 0x02 : 0x00) |
                (button == MouseButtons.Middle ? 0x04 : 0x00));
            var cmd = new byte[] { 0x57, 0xab, 0x00, 0x05, 0x05, 0x01, btn, xc, yc, deltac, 0x00 };
            cmd[cmd.Length - 1] = (byte)(cmd.Sum(x => x) & 0xff);
            _serialPort.Write(cmd, 0, cmd.Length);
            var buf = new byte[7];
            _serialPort.Read(buf, 0, buf.Length);
        }

        /// <summary>
        /// キー入力
        /// </summary>
        bool _altDown = false;
        bool _ctrlDown = false;
        bool _shiftDown = false;
        List<int> _downKeys = new();
        private void SendKeyStatus()
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            var specialKey = (byte)((_altDown ? 0x04 : 0x00) |
                (_ctrlDown ? 0x01 : 0x00) |
                (_shiftDown ? 0x02 : 0x00));
            var downKeys = _downKeys.Skip(Math.Max(0, _downKeys.Count - 6)).ToList();
            downKeys.AddRange(Enumerable.Repeat(0, 6));

            var cmd = new byte[] { 0x57, 0xab, 0x00, 0x02, 0x08, (byte)specialKey, 0x00,
                (byte)downKeys[0], (byte)downKeys[1], (byte)downKeys[2], (byte)downKeys[3], (byte)downKeys[4], (byte)downKeys[5], 0x00 };
            cmd[cmd.Length - 1] = (byte)(cmd.Sum(x => x) & 0xff);
            _serialPort.Write(cmd, 0, cmd.Length);
            var buf = new byte[7];
            _serialPort.Read(buf, 0, buf.Length);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            Debug.WriteLine($"KeyDown: {e.KeyCode} {e.KeyData}");
            if (e.KeyCode == Keys.Menu) _altDown = true;
            else if (e.KeyCode == Keys.ControlKey) _ctrlDown = true;
            else if (e.KeyCode == Keys.ShiftKey) _shiftDown = true;
            else
            {
                var key = KeyConvert(e.KeyCode, e.KeyData);
                if (!_downKeys.Contains(key))
                    _downKeys.Add(key);
                SendKeyStatus();
            }
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            Debug.WriteLine($"KeyUp: {e.KeyCode} {e.KeyData}");
            if (e.KeyCode == Keys.Menu) _altDown = false;
            else if (e.KeyCode == Keys.ControlKey) _ctrlDown = false;
            else if (e.KeyCode == Keys.ShiftKey) _shiftDown = false;
            else
            {
                var key = KeyConvert(e.KeyCode, e.KeyData);
                if (_downKeys.Contains(key))
                    _downKeys.Remove(key);
                SendKeyStatus();
            }
        }

        byte KeyConvert(Keys code, Keys data)
        {
            switch (code)
            {
                // ファンクションブロック
                case Keys.Escape: return 0x58;
                case Keys.F1: return 0x3a;
                case Keys.F2: return 0x3b;
                case Keys.F3: return 0x3c;
                case Keys.F4: return 0x3d;
                case Keys.F5: return 0x3e;
                case Keys.F6: return 0x3f;
                case Keys.F7: return 0x40;
                case Keys.F8: return 0x41;
                case Keys.F9: return 0x42;
                case Keys.F10: return 0x43;
                case Keys.F11: return 0x44;
                case Keys.F12: return 0x45;
                case Keys.PrintScreen: return 0x46;
                case Keys.Scroll: return 0x47;
                case Keys.Pause: return 0x48;

                // メインキーボードブロック
                case Keys.D1: return 0x1e;
                case Keys.D2: return 0x1f;
                case Keys.D3: return 0x20;
                case Keys.D4: return 0x21;
                case Keys.D5: return 0x22;
                case Keys.D6: return 0x23;
                case Keys.D7: return 0x24;
                case Keys.D8: return 0x25;
                case Keys.D9: return 0x26;
                case Keys.D0: return 0x27;

                case Keys.A: return 0x04;
                case Keys.B: return 0x05;
                case Keys.C: return 0x06;
                case Keys.D: return 0x07;
                case Keys.E: return 0x08;
                case Keys.F: return 0x09;
                case Keys.G: return 0x0a;
                case Keys.H: return 0x0b;
                case Keys.I: return 0x0c;
                case Keys.J: return 0x0d;
                case Keys.K: return 0x0e;
                case Keys.L: return 0x0f;
                case Keys.M: return 0x10;
                case Keys.N: return 0x11;
                case Keys.O: return 0x12;
                case Keys.P: return 0x13;
                case Keys.Q: return 0x14;
                case Keys.R: return 0x15;
                case Keys.S: return 0x16;
                case Keys.T: return 0x17;
                case Keys.U: return 0x18;
                case Keys.V: return 0x19;
                case Keys.W: return 0x1a;
                case Keys.X: return 0x1b;
                case Keys.Y: return 0x1c;
                case Keys.Z: return 0x1d;

                case Keys.OemMinus: return 0x2d;    // =-
                case Keys.Oem7: return 0x2e;    // ~^
                case Keys.Oem5: return 0x89;    // |\
                case Keys.Back: return 0x2a;

                case Keys.Oemtilde: return 0x2f;    // @`
                case Keys.OemOpenBrackets: return 0x30;    // [{

                case Keys.Oemplus: return 0x33;    // ;+
                case Keys.Oem1: return 0x34;    // :*
                case Keys.Oem6: return 0x31;    // ]}

                case Keys.Oemcomma: return 0x36;    // ,<
                case Keys.OemPeriod: return 0x37;    // .>


                case Keys.OemQuestion: return 0x38;    // /?
                case Keys.OemBackslash: return 0x87;    // _^

                // TODO 半角全角
                case Keys.Tab: return 0x2b;
                case Keys.Capital: return 0x39;
                case Keys.Return: return 0x28;
                case Keys.Space: return 0x2c;
                case Keys.IMENonconvert: return 0x8b;
                case Keys.IMEConvert: return 0x8a;
                case Keys.Apps: return 0x65;

                case Keys.Insert: return 0x49;
                case Keys.Delete: return 0x4c;
                case Keys.Home: return 0x4a;
                case Keys.End: return 0x4d;
                case Keys.Next: return 0x4e;
                case Keys.PageUp: return 0x4b;

                case Keys.Down: return 0x51;
                case Keys.Up: return 0x52;
                case Keys.Left: return 0x50;
                case Keys.Right: return 0x4f;

                case Keys.NumPad0: return 0x62;
                case Keys.NumPad1: return 0x59;
                case Keys.NumPad2: return 0x5a;
                case Keys.NumPad3: return 0x5b;
                case Keys.NumPad4: return 0x5c;
                case Keys.NumPad5: return 0x5d;
                case Keys.NumPad6: return 0x5e;
                case Keys.NumPad7: return 0x5f;
                case Keys.NumPad8: return 0x60;
                case Keys.NumPad9: return 0x61;
                case Keys.NumLock: return 0x53;
                case Keys.Divide: return 0x54;
                case Keys.Multiply: return 0x55;
                case Keys.Subtract: return 0x56;
                case Keys.Add: return 0x57;
                case Keys.Decimal: return 0x63;
            }
            return 0x00;
        }
    }
}