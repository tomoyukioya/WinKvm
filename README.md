# WinKvm

CH9329（USB HID エミュレータ）と USB ビデオキャプチャを組み合わせた、Windows 用のソフトウェア KVM コンソールです。

> **Note**
> ここでの KVM は **Keyboard / Video / Mouse** の意味です。Linux の KVM（カーネル仮想化）とは関係ありません。

## これは何か

操作対象 PC の映像出力を USB キャプチャ経由でウィンドウに表示し、そのウィンドウ上でのキーボード・マウス操作を CH9329 経由で USB HID デバイスとして操作対象 PC に送り返します。

```
映像:  [操作対象 PC] --HDMI--> [USB キャプチャ] --USB--> [Windows PC / WinKvm]
操作:  [操作対象 PC] <--USB HID-- [CH9329] <--USB シリアル-- [Windows PC / WinKvm]
```

操作対象 PC 側からは単なる USB キーボード / マウスとして見えるため、**ドライバもエージェントも不要**で、BIOS/UEFI 設定画面や OS 起動前の状態でも操作できます。

## 必要なハードウェア

| 用途 | 例 |
| --- | --- |
| USB HID エミュレータ | CH9329 搭載モジュール（USB シリアル ⇔ USB HID 変換） |
| 映像取り込み | UVC 対応の USB HDMI キャプチャ |

## 動作環境

- Windows
- [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)

## 使い方

1. WinKvm.exe を起動します。
2. メニューの **「マウス/キーボード設定」** から、CH9329 が接続されている COM ポートを選択します。
3. メニューの **「ビデオ設定」** から、キャプチャデバイスと解像度／フレームレート／FourCC を選択します。
4. 同じく「ビデオ設定」から表示倍率（50%〜200%）を変更できます。

選択したポート・デバイス・解像度・倍率は自動保存され、次回起動時に復元されます。

映像の上にマウスカーソルを乗せている間はカーソルが非表示になり、キーボード・マウス操作が操作対象 PC に転送されます。

### 表示のトリミング

キャプチャの上下左右を切り落としたい場合は、設定の `TrimTop` / `TrimLeft` / `TrimRight` / `TrimBottom` にピクセル数を指定します（GUI からの設定には未対応です）。初期値は [`WinKvm/App.config`](WinKvm/App.config) にありますが、一度アプリを起動して設定を保存すると、以降はユーザー単位の `user.config` が優先されます。

## CH9329 との通信

9600bps / 8-N-1 で以下のコマンドを送信しています。末尾 1 バイトはそれ以前の全バイトの総和の下位 8 ビット（チェックサム）です。

| 機能 | 先頭バイト列 |
| --- | --- |
| キーボード | `57 AB 00 02 08 ...` |
| マウス（相対座標） | `57 AB 00 05 05 01 ...` |
| マウス（絶対座標） | `57 AB 00 04 07 02 ...` |

絶対座標は画面全体を 0〜4096 に正規化した値で送ります。実装は [`WinKvm/MainForm.cs`](WinKvm/MainForm.cs) の `MoveMouseAbsolute` / `MoveMouseRelative` / `SendKeyStatus` を参照してください。

## ビルド

```
dotnet build WinKvm.sln -c Release
```

Visual Studio 2022 でも `WinKvm.sln` をそのまま開けます。

主な依存パッケージ（すべて NuGet から復元されます）:

- [OpenCvSharp4](https://github.com/shimat/opencvsharp) — 映像の取り込みと描画
- [DirectShowLib.Standard](https://www.nuget.org/packages/DirectShowLib.Standard/) — キャプチャデバイスと対応解像度の列挙
- [NReco.Logging.File](https://github.com/nreco/logging) — ファイルログ出力

ログの出力先とログレベルは [`WinKvm/appsettings.json`](WinKvm/appsettings.json) で変更できます。

## 既知の制限

- ターゲットフレームワークが `net6.0-windows` のままです（.NET 6 はサポート終了済み）。
- キーコード変換は日本語キーボード配列を前提としています。
- 半角/全角キー、Windows キーには未対応です。
- 修飾キーは Ctrl / Alt / Shift のみ扱います。

## 参考

### CH9329 のドキュメント

上記コマンドの仕様は **データシートには記載されていません**。データシート自体が別文書を参照するよう指示しています（"For the specific protocol format, refer to `CH9329 Serial Communication Protocol_Vx.x.PDF`"）。

| 文書 | 内容 | 入手先 |
| --- | --- | --- |
| CH9329 データシート<br>(`CH9329DS1.PDF` V1.1) | ピン配置、動作モード、電気特性 | [秋月電子](https://akizukidenshi.com/goodsaffix/ch9329.pdf) / [alldatasheet](https://www.alldatasheet.com/datasheet-pdf/pdf/1148630/WCH/CH9329.html) |
| CH9329 芯片串口通信协议<br>(V1.2、中国語) | **シリアルコマンドのフレーム仕様**。本アプリの実装はこれに基づく | メーカー (WCH) 配布の `CH9329EVT.ZIP` に同梱。[Gitee 上のコピー](https://gitee.com/dsiclu/mouse/blob/master/CH9329%E8%8A%AF%E7%89%87%E4%B8%B2%E5%8F%A3%E9%80%9A%E4%BF%A1%E5%8D%8F%E8%AE%AE.PDF)（第三者リポジトリ） |
| キーボード／マウス エミュレータ 解説書<br>（みんなのラボ、日本語） | CH9329 搭載モジュール `MR-CH9329EMU` の解説。コマンドの日本語での解説があり、最初に読むならこれが分かりやすい | [マルツ電子](https://www.marutsu.co.jp/contents/shop/marutsu/datasheet/minnanolab_MR-CH9329EMU.pdf) |

### ツール

- [hry2566/v4w2-ctl](https://github.com/hry2566/v4w2-ctl) — Windows 版の `v4l2-ctl` 相当。キャプチャデバイス名や対応フォーマットを事前に調べるのに便利です（WinKvm の動作には不要）

## ライセンス

[MIT License](LICENSE)
