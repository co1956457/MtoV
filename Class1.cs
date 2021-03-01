// https://github.com/CommentViewerCollection/MultiCommentViewer    BouyomiPlugin を参考に作成                           GPL-3.0 License 
// https://github.com/oocytanb                                      CommentBaton から縦書きコメビュにメッセージを送る    MIT License
//
// SPDX-License-Identifier: GPL-3.0    // ベースにした MultiCommentViewer の BouyomiPlugin が GPL-3.0 なので。
//
// 20200725 v1.0 Taki co1956457
//               cytanb ver. Commits on Jul 24, 2020.
// 20201003 v1.1 cytanbをモジュール化 (ver. Commits on Sep 29, 2020)
// 20201003 v1.2 設定ファイルの改行対応
// 20201004 v1.3 local cytanb -> cytanb
// 20210213 v1.4 Mixer をコメントアウト Comment out the Mixer
// 20210218 v2.0 転送モード Transfer mode
// 20210301 v2.1 7000 -> 8000ms　スーパーチャットコメント修正 fixed YouTube Live super chat comment

using System;
using System.ComponentModel.Composition;    // [Export(typeof(IPlugin))]
using System.IO;                            // File, Directory
using System.Collections.Generic;           // List
using System.Windows.Forms;                 // MessageBox
using System.Timers;                        // Timer
using System.Linq;                          // Last

// Plugin共通
using Plugin;
using SitePlugin;
using PluginCommon; // ToText

// dll の参照追加は ***SitePlugin だけでなく ***IF も必要
// ニコ生・ショールーム・ユーチューブ・ツイキャス
// 「NicoSitePlugin」ではなく「NicoSitePlugin2」 を参照追加すること
using NicoSitePlugin;
using ShowRoomSitePlugin;
using YouTubeLiveSitePlugin;
using TwicasSitePlugin;
using TwitchSitePlugin;
using OpenrecSitePlugin;
using WhowatchSitePlugin;

// 確認できなかったのでコメントアウト
//using LineLiveSitePlugin;
//using MildomSitePlugin;

namespace MtoVPlugin
{
    // Youtube Live (Stickers) と TwitCasting 用
    // BouyomiPlugin の main.cs から
    static class MessageParts
    {
        public static string ToTextWithImageAlt(this IEnumerable<IMessagePart> parts)
        {
            string s = "";
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    if (part is IMessageText text)
                    {
                        s += text;
                    }
                    else if (part is IMessageImage image)
                    {
                        s += image.Alt;
                    }
                }
            }
            return s;
        }
    }

    /// <summary>
    /// MtoV 本体
    /// </summary>
    [Export(typeof(IPlugin))]
    public class Class1 : IPlugin, IDisposable
    {
        public string Name { get { return "MtoV 設定(Settings)"; } }
        public string Description { get { return "MCVからVCへコメント転送"; } }
        public IPluginHost Host { get; set; }

        // Form用
        private Form1 _form1;

        // ファイル存在確認エラー用
        int fileExist;

        // プラグインの状態
        // transferMode
        //  0:転送しない　off
        //  1:部分転送  　スタジオモード（ニコ生：運営のみ　SH：転送なし　他YtTc等：転送）    Studio mode (Ni:Control command, SH:Off, Yt,Tc,etc:On)
        //  2:全転送 ALL  ルームモード room mode
        public int transferMode;
        
        // 起動時にだけファイルから転送モードを読み込む
        private int initialRead = 0;

        // CommentBaton のディレクトリ用
        string targetDirectory;
        // CommentBaton のパス
        string targetPath;
        // 設定ファイル のパス
        string readPath;

        // 送信コメントをためておく
        List<string> emitCmnt = new List<string>();
        List<string> buffEmit = new List<string>();

        // 起動後最初の接続判定用
        int connected = 0;

        // タイマーの生成
        System.Timers.Timer timer = new System.Timers.Timer();

        /// <summary>
        /// 起動時
        /// </summary>
        public virtual void OnLoaded()
        {
            // ファイルの存在確認
            fileExist = fileExistError();

            if (fileExist == 0) // 問題なし
            {
                initialRead = 1;
                if (transferMode > 0) // 前回の設定:コメント転送ON
                {
                    // main.lua 初期化
                    File.WriteAllText(targetPath, "");

                    // タイマー設定
                    // コメントがないときに一時停止する方法は保留
                    timer.Elapsed += new ElapsedEventHandler(OnElapsed_TimersTimer);
                    timer.Interval = 8000;

                    // タイマー開始
                    timer.Start();
                }
            }
            else // 問題あり
            {
                showFileExistError(fileExist);
            }
        }

        /// <summary>
        /// 設定→常に一番手前に表示を選んだ時
        /// </summary>
        public void OnTopmostChanged(bool isTopmost)
        {
            // MessageBox.Show("OnTopmostChanged");
        }

        /// <summary>
        /// プラグイン→ MtoV 設定(Settings) を選んだ時
        /// </summary>
        public void ShowSettingView()
        {
            //フォームの生成
            _form1 = new Form1(this);
            _form1.Text = "MtoV 設定(Settings)";
            _form1.Show();
            _form1.FormClosed += new FormClosedEventHandler(_form1_FormClosed);
        }

        /// <summary>
        /// 閉じるボタン☒を押した時
        /// </summary>
        public void OnClosing()
        {
            // ファイルの存在確認
            fileExist = fileExistError();

            if (fileExist == 0) // 問題なし
            {
                // main.lua 初期化
                File.WriteAllText(targetPath, "");
                // MtoV.txtに設定情報 transferMode を保存
                File.WriteAllText(readPath, targetDirectory + Environment.NewLine + transferMode);
            }
            else // 問題あり
            {
                // do nothing
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
             //MessageBox.Show("Dispose");
        }

        /// <summary>
        /// コメントを受信したら書き出すまでためておく
        /// </summary>
        public void OnMessageReceived(ISiteMessage message, IMessageMetadata messageMetadata)
        {
            if (transferMode > 0) // 稼働中
            {
                var (name, comment, cmntSource) = GetData(message);

                // nameがnullでは無い場合かつUser.Nicknameがある場合はNicknameを採用
                if (!string.IsNullOrEmpty(name) && messageMetadata.User != null && !string.IsNullOrEmpty(messageMetadata.User.Nickname))
                {
                    name = messageMetadata.User.Nickname;
                }

                string _name = name;
                string _comment = comment;
                string _cmntSource = cmntSource;

                if (transferMode == 0) // 転送しない
                { }
                else if ((transferMode == 1) && (_cmntSource == "Nicolive")) // スタジオ ニコ生運営コメのみ転送　一般コメ転送しない
                { }
                else if ((transferMode == 1) && (_cmntSource == "Showroom")) // スタジオ ショールームコメ転送しない
                { }
                else // 全転送 (transferMode ==2)
                {
                    // 追加
                    buffEmit.Add("    cytanb.EmitCommentMessage(\'" + _comment + "\', {name = \'" + _name + "\', commentSource = \'" + _cmntSource + "\'})");
                }
            }
            else
            {
                // do nothing
            }
        }

        /// <summary>
        /// コメントがあれば指定時間ごとに main.lua に書き出す
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnElapsed_TimersTimer(object sender, ElapsedEventArgs e)
        {
            emitCmnt = new List<string>(buffEmit);
            buffEmit.Clear();

            // 新しいコメントがあれば main.lua 上書き
            if(emitCmnt[0] != "")
            {
                string s1;
                string s2;
                string s3;

                // cytanb.EmitCommentMessage の実行は IsMine の中に書くこと。そうしないとゲストの人数分実行されてしまう。
                // cytanb ver. Commits on Sep 29, 2020
                // \ -> \\      ' -> \'     " -> \"
                s1 = "cytanb = cytanb or require(\'cytanb\')(_ENV)\n\nif vci.assets.IsMine then\n";

                // 接続時は最新データーを１件
                // それ以外はたまっていたもの全部
                // ※再接続判定難あり。
                // 　NCVのような「接続」「切断」のイベントの取り方不明
                if (connected == 0)
                {
                    s2 = emitCmnt.Last();
                    connected = 1;
                }
                else
                {
                    s2 = string.Join("\n", emitCmnt);
                }
                emitCmnt.Clear();


                // 念のため最後に \n を入れておく
                // タイミングによっては Visual Studio Code で警告が出る場合がある？　よくわからない
                // なくても正常に動作はする
                s3 = "\nend\n";

                File.WriteAllText(targetPath, s1 + s2 + s3);
            }
            else
            {
                // do nothing
            }
        }

        /// <summary>
        /// 名前とコメントを取得
        /// 参考：BouyomiPlugin
        /// </summary>
        private static (string name, string comment, string cmtSource) GetData(ISiteMessage message)
        {
            string name = "（名前なし）";
            string comment = "（本文なし）";
            string cmntSource = "Unknown";

            // ニコ生運営コメント cmntSource = "NCV" NCVからしか転送していなかった名残り
            // CommentBatonを利用した既存VCIに影響があるのでこのままにしておく
            // Connected/Disconnected の効果不明
            //
            if (message is INicoMessage NicoMessage)
            {
                name = "（運営）";
                cmntSource = "NCV";
                switch (NicoMessage.NicoMessageType)
                {
                    case NicoMessageType.Connected:
                        comment = (NicoMessage as INicoConnected).Text;
                        break;
                    case NicoMessageType.Disconnected:
                        comment = (NicoMessage as INicoDisconnected).Text;
                        break;
                    case NicoMessageType.Item:
                        comment = (NicoMessage as INicoGift).Text;
                        break;
                    case NicoMessageType.Ad:
                        comment = (NicoMessage as INicoAd).Text;
                        break;
                    case NicoMessageType.Spi:
                        comment = (NicoMessage as INicoSpi).Text;
                        break;
                    case NicoMessageType.Info:
                        comment = (NicoMessage as INicoInfo).Text;
                        break;
                    case NicoMessageType.Emotion:
                        // エモーション判定のために "／emotion " をつけておく　NCVプラグインの影響
                        comment = "／emotion " + (NicoMessage as INicoEmotion).Content;
                        break;
                    case NicoMessageType.Comment:
                        if ((NicoMessage as INicoComment).Is184 == true)
                        {
                            name = "";
                        }
                        else
                        {
                            name = (NicoMessage as INicoComment).UserName;
                        }
                        comment = (NicoMessage as INicoComment).Text;
                        cmntSource = "Nicolive";
                        break;
                }
            }
            // ショールーム：全転送時　（ギフト等の判定できない？）
            else if (message is IShowRoomMessage showroomMessage)
            {
                //name = "（運営）";
                cmntSource = "Showroom";
                switch (showroomMessage.ShowRoomMessageType)
                {
                    case ShowRoomMessageType.Comment:
                        name = (showroomMessage as IShowRoomComment).UserName;
                        comment = (showroomMessage as IShowRoomComment).Text;
                        break;
                }
            }
            // Youtube Live
            else if (message is IYouTubeLiveMessage youTubeLiveMessage)
            {
                name = "（運営）";
                cmntSource = "Youtubelive";
                switch (youTubeLiveMessage.YouTubeLiveMessageType)
                {
                    case YouTubeLiveMessageType.Connected:
                        // name = null;
                        comment = (youTubeLiveMessage as IYouTubeLiveConnected).Text;
                        break;
                    case YouTubeLiveMessageType.Disconnected:
                        // name = null;
                        comment = (youTubeLiveMessage as IYouTubeLiveDisconnected).Text;
                        break;
                    case YouTubeLiveMessageType.Comment:
                        name = (youTubeLiveMessage as IYouTubeLiveComment).NameItems.ToText();
                        // comment = (youTubeLiveMessage as IYouTubeLiveComment).CommentItems.ToText();
                        comment = (youTubeLiveMessage as IYouTubeLiveComment).CommentItems.ToTextWithImageAlt();
                        break;
                    case YouTubeLiveMessageType.Superchat:
                        name = (youTubeLiveMessage as IYouTubeLiveSuperchat).NameItems.ToText();
                        // comment = (youTubeLiveMessage as IYouTubeLiveSuperchat).CommentItems.ToText();
                        comment = (youTubeLiveMessage as IYouTubeLiveSuperchat).PurchaseAmount + " " + (youTubeLiveMessage as IYouTubeLiveSuperchat).CommentItems.ToTextWithImageAlt();
                        // 縦書きコメビュで強調できるように cmntSource を変えておく
                        cmntSource = "YoutubeliveSC";
                        break;
                }
            }
            // TwitCasting
            else if (message is ITwicasMessage twicasMessage)
            {
                name = "（運営）";
                cmntSource = "Twitcasting";
                switch (twicasMessage.TwicasMessageType)
                {
                    case TwicasMessageType.Connected:
                        // name = null;
                        comment = (twicasMessage as ITwicasConnected).Text;
                        break;
                    case TwicasMessageType.Disconnected:
                        // name = null;
                        comment = (twicasMessage as ITwicasDisconnected).Text;
                        break;
                    case TwicasMessageType.Comment:
                        name = (twicasMessage as ITwicasComment).UserName;
                        comment = (twicasMessage as ITwicasComment).CommentItems.ToText();
                        break;
                    case TwicasMessageType.Item:
                        name = (twicasMessage as ITwicasItem).UserName;
                        comment = (twicasMessage as ITwicasItem).CommentItems.ToTextWithImageAlt();
                        break;
                }
            }

            // Openrec
            else if (message is IOpenrecMessage openrecMessage)
            {
                name = "（運営）";
                cmntSource = "Openrec";
                switch (openrecMessage.OpenrecMessageType)
                {
                    case OpenrecMessageType.Connected:
                        // name = null;
                        comment = (openrecMessage as IOpenrecConnected).Text;
                        break;
                    case OpenrecMessageType.Disconnected:
                        // name = null;
                        comment = (openrecMessage as IOpenrecDisconnected).Text;
                        break;
                    case OpenrecMessageType.Comment:
                        name = (openrecMessage as IOpenrecComment).NameItems.ToText();
                        comment = (openrecMessage as IOpenrecComment).MessageItems.ToText();
                        break;
                }
            }
            // Twitch
            else if (message is ITwitchMessage twitchMessage)
            {
                name = "（運営）";
                cmntSource = "Twitch";
                switch (twitchMessage.TwitchMessageType)
                {
                    case TwitchMessageType.Connected:
                        // name = null;
                        comment = (twitchMessage as ITwitchConnected).Text;
                        break;
                    case TwitchMessageType.Disconnected:
                        // name = null;
                        comment = (twitchMessage as ITwitchDisconnected).Text;
                        break;
                    case TwitchMessageType.Comment:
                        name = (twitchMessage as ITwitchComment).DisplayName;
                        comment = (twitchMessage as ITwitchComment).CommentItems.ToText();
                        break;
                }
            }
            // Whowatch
            else if (message is IWhowatchMessage whowatchMessage)
            {
                name = "（運営）";
                cmntSource = "Whowatch";
                switch (whowatchMessage.WhowatchMessageType)
                {
                    case WhowatchMessageType.Connected:
                        // name = null;
                        comment = (whowatchMessage as IWhowatchConnected).Text;
                        break;
                    case WhowatchMessageType.Disconnected:
                        // name = null;
                        comment = (whowatchMessage as IWhowatchDisconnected).Text;
                        break;
                    case WhowatchMessageType.Comment:
                        name = (whowatchMessage as IWhowatchComment).UserName;
                        comment = (whowatchMessage as IWhowatchComment).Comment;
                        break;
                    case WhowatchMessageType.Item:
                        name = (whowatchMessage as IWhowatchItem).UserName;
                        comment = (whowatchMessage as IWhowatchItem).Comment + "[" + (whowatchMessage as IWhowatchItem).ItemName + "x" + (whowatchMessage as IWhowatchItem).ItemCount.ToString() + "]";
                        break;
                }
            }

            //// 確認できなかったのでコメントアウト
            //// LineLive
            //else if (message is ILineLiveMessage lineLiveMessage)
            //{
            //    name = "（運営）";
            //    cmntSource = "Linelive";
            //    switch (lineLiveMessage.LineLiveMessageType)
            //    {
            //        case LineLiveMessageType.Connected:
            //            // name = null;
            //            comment = (lineLiveMessage as ILineLiveConnected).Text;
            //            break;
            //        case LineLiveMessageType.Disconnected:
            //            // name = null;
            //            comment = (lineLiveMessage as ILineLiveDisconnected).Text;
            //            break;
            //        case LineLiveMessageType.Comment:
            //            name = (lineLiveMessage as ILineLiveComment).DisplayName;
            //            comment = (lineLiveMessage as ILineLiveComment).Text;
            //            break;
            //    }
            //}
            //// Mildom
            //else if (message is IMildomMessage MildomMessage)
            //{
            //    name = "（運営）";
            //    cmntSource = "Mildom";
            //    switch (MildomMessage.MildomMessageType)
            //    {
            //        case MildomMessageType.Connected:
            //           // name = null;
            //            comment = (MildomMessage as IMildomConnected).Text;
            //            break;
            //        case MildomMessageType.Disconnected:
            //            // name = null;
            //            comment = (MildomMessage as IMildomDisconnected).Text;
            //            break;
            //        case MildomMessageType.Comment:
            //            name = (MildomMessage as IMildomComment).UserName;
            //            comment = (MildomMessage as IMildomComment).CommentItems.ToText();
            //            break;
            //        case MildomMessageType.JoinRoom:
            //            // name = null;
            //            comment = (MildomMessage as IMildomJoinRoom).CommentItems.ToText();
            //            break;
            //            //case MildomMessageType.Leave:
            //            //        name = null;
            //            //        comment = (MildomMessage as IMildomLeave).CommentItems.ToText();
            //            //    break;
            //    }
            //}

            // YouTube Live SC 等改行が入ることがある \r 置換が有効
            // コメント中の「'」に要注意　's など英語コメントでよく入る
            comment = comment.Replace("\n", "　").Replace("\r", "　").Replace("\'", "’").Replace("\"", "”").Replace("\\", "＼");
            comment = comment.Replace("$", "＄").Replace("/", "／").Replace(",", "，");
            name = name.Replace("\n", "　").Replace("\r", "　").Replace("\'", "’").Replace("\"", "”").Replace("\\", "＼");
            name = name.Replace("$", "＄").Replace("/", "／").Replace(",", "，");
            
            // 念のため
            if (name == null)
            {
                name = "（名前なし）";
            }
            if (comment == null)
            {
                comment = "（本文なし）";
            }
            if (cmntSource == null)
            {
                cmntSource = "Unknown";
            }

            return (name, comment, cmntSource);
        }

        /// <summary>
        /// ファイルの存在確認
        /// </summary>
        int fileExistError()
        {
            // 値を返す用
            int returnInt;
            // カレントディレクトリ（MCV 実行ディレクトリ）
            string curDirectory = Environment.CurrentDirectory;
            // プラグインディレクトリ
            curDirectory = curDirectory + "\\plugins\\MtoV";
            // 設定ファイル名
            readPath = curDirectory + "\\MtoV.txt";
            // main.lua
            string targetName;
            targetName = "\\main.lua";

            // ファイルの存在確認
            if (File.Exists(readPath)) // 設定ファイルあり
            {
                // 行ごとの配列として、テキストファイルの中身をすべて読み込む
                string[] lines = File.ReadAllLines(readPath);

                // 最後に終了コード999を追記 転送モード初設定判定用
                string[] settingLines = new string[lines.Length + 1];
                Array.Copy(lines, settingLines, lines.Length);
                settingLines[lines.Length] = "999";

                if (initialRead == 0) // 起動時のみファイルから転送モード読み込み
                {
                    // transferMode
                    //  0:転送しない　off
                    //  1:部分転送  　スタジオモード（ニコ生：運営のみ　SH：転送なし　他YtTc等：転送）    Studio mode (Ni:Control command, SH:Off, Yt,Tc,etc:On)
                    //  2:全転送 ALL  ルームモード room mode
                    //
                    if (settingLines[1] == "0" || settingLines[1] == "1" || settingLines[1] == "2")
                    {
                        transferMode = int.Parse(settingLines[1]);
                    }
                    else
                    {
                        transferMode = 1; // initial setting
                    }
                }
                // ディレクトリ確認
                targetDirectory = lines[0];
                targetDirectory = targetDirectory.Replace("\r", "").Replace("\n", "");　// 設定ファイルの改行を削除
                string[] strF = targetDirectory.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

                if (strF[strF.Length - 1] == "CommentBaton") // フォルダ名が CommentBaton
                {
                    if (Directory.Exists(targetDirectory) == false)
                    {
                        returnInt = 3; // 設定ファイルあり　名前は CommentBaton　指定ディレクトリなし
                    }
                    else // 設定ファイルあり　名前は CommentBaton　指定ディレクトリあり
                    {
                        // main.lua 存在確認
                        targetPath = targetDirectory + targetName;
                        if (File.Exists(targetPath) == false) // なかったら
                        {
                            // main.lua 作成
                            File.WriteAllText(targetPath, "");
                        }
                        returnInt = 0; // 問題なし
                    }
                }
                else
                {
                    returnInt = 2; // 設定ファイルあり 名前が違う
                }
            }
            else
            {
                returnInt = 1; // 設定ファイルなし
            }
            return returnInt;
        }

        /// <summary>
        /// エラー表示
        /// </summary>
        void showFileExistError(int errorNumber)
        {
            if (errorNumber == 1)
            {
                // プラグイン停止
                transferMode = 0;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n設定ファイルがありません。\nThere is no setting file.\n\n1. …\\MultiCommentViewer\\plugins\\MtoV\\MtoV.txt を作成してください。\n   Please create the text file.\n\n2. MtoV.txt に CommentBaton VCI の場所 C:\\Users\\%ユーザー名%\\AppData\\LocalLow\\infiniteloop Co,Ltd\\VirtualCast\\EmbeddedScriptWorkspace\\CommentBaton を書いてください。\n   Please write the CommentBaton VCI directory in the text file.\n\n3. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
            else if (errorNumber == 2)
            {
                // プラグイン停止
                transferMode = 0;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n指定ディレクトリが CommentBaton ではありません。\nThe directory is not CommentBaton.\n\n1. MtoV.txt の内容（ CommentBaton VCI の場所 C:\\Users\\%ユーザー名%\\AppData\\LocalLow\\infiniteloop Co,Ltd\\VirtualCast\\EmbeddedScriptWorkspace\\CommentBaton ）を確認してください。\n   Please check the CommentBaton directory in the MtoV.txt.\n\n2. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
            else if (errorNumber == 3)
            {
                // プラグイン停止
                transferMode = 0;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n指定ディレクトリがありません。\nThe directory does not Exist.\n\n1. MtoV.txt の内容（ CommentBaton VCI の場所 C:\\Users\\%ユーザー名%\\AppData\\LocalLow\\infiniteloop Co,Ltd\\VirtualCast\\EmbeddedScriptWorkspace\\CommentBaton ）と実在を確認してください。\n   Please check the CommentBaton directory in the MtoV.txt and existence.\n\n2. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
        }

        //フォームが閉じられた時のイベントハンドラ
        void _form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            int old_transferMode = transferMode; 
            transferMode = _form1.tMode;

            if (old_transferMode > 0 && transferMode == 0)
            {
                // タイマー停止
                timer.Stop();
                // main.lua 初期化
                File.WriteAllText(targetPath, "");
            }
            else if (old_transferMode == 0 && transferMode > 0)
            {
                // タイマー開始
                timer.Start();
                // main.lua 初期化
                File.WriteAllText(targetPath, "");
            }

            if (old_transferMode != transferMode)
            {
                // 設定ファイルにパスとモードを保存
                File.WriteAllText(readPath, targetDirectory + Environment.NewLine + transferMode);
            }

            //フォームが閉じられた時のイベントハンドラ削除
            _form1.FormClosed -= _form1_FormClosed;
            _form1 = null;
        }
    }
}
