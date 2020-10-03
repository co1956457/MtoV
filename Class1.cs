// https://higehito.qrunch.io/entries/2eqRPvFL8sWpKIR3              この記事を参考に基本的な参照等追加                   webの公開情報
// https://github.com/CommentViewerCollection/MultiCommentViewer    BouyomiPlugin を参考に作成                           GPL-3.0 License 
// https://github.com/oocytanb                                      CommentBaton から縦書きコメビュにメッセージを送る    MIT License
//
// SPDX-License-Identifier: GPL-3.0   // MultiCommentViewer の BouyomiPlugin が GPL-3.0 なので。
// 20200725 v1.0 Taki co1956457
//               cytanb ver. Commits on Jul 24, 2020.
// 20201003 v1.1 cytanbをモジュール化 (ver. Commits on Sep 29, 2020)
// 20201003 v1.2 設定ファイルの改行対応
//
using System;
using System.IO;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Timers;

using Plugin;
using SitePlugin;
using PluginCommon; // ToText

// dll の参照追加は ***SitePlugin だけでなく ***IF も必要
// SHOWROOM：デフォルト対応 コメントの転送必要なし
// ニコ生：運営コメが取得ができない？
// case NicoMessageType.Comment: OK
// case NicoMessageType.Item Ad Info Unknown Kick Ignored 反応なし
//
using YouTubeLiveSitePlugin;
using TwicasSitePlugin;
using LineLiveSitePlugin;
using MildomSitePlugin;
using MirrativSitePlugin;
using MixerSitePlugin;
using OpenrecSitePlugin;
using PeriscopeSitePlugin;
using TwitchSitePlugin;
using WhowatchSitePlugin;
using System.Linq;

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
    public class MtoVPlugin : IPlugin, IDisposable
    {
        public string Name { get { return "MtoV [停止/開始]"; } }
        public string Description { get { return "ＭからＶへ　コメントを送る"; } }
        public IPluginHost Host { get; set; }

        // プラグインの起動・停止
        bool ONOFF = true;

        // ファイル存在確認エラー用
        int fileExist;

        // CommentBaton のパス
        string targetPath;

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
                ONOFF = true;
                // main.lua 初期化
                File.WriteAllText(targetPath, "");

                // タイマー設定
                timer.Elapsed += new ElapsedEventHandler(OnElapsed_TimersTimer);
                timer.Interval = 7000;

                // タイマー開始
                timer.Start();
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
        /// プラグイン→ MtoV を選んだ時
        /// </summary>
        public void ShowSettingView()
        {
            // ファイルの存在確認
            fileExist = fileExistError();

            if (fileExist == 0) // 問題なし
            {
                // 稼働中なら停止　停止中なら開始
                if (ONOFF)
                {
                    // プラグイン停止
                    ONOFF = false;
                    // タイマー停止
                    timer.Stop();
                    MessageBox.Show("停止しました。\n\nStopped", "MtoV");
                    // main.lua 初期化
                    File.WriteAllText(targetPath, "");
                }
                else
                {
                    // プラグイン開始
                    ONOFF = true;
                    // タイマー開始
                    timer.Start();
                    MessageBox.Show("開始しました。\n\nStarted", "MtoV");
                    // main.lua 初期化
                    File.WriteAllText(targetPath, "");
                }
            }
            else // 問題あり
            {
                showFileExistError(fileExist);
            }
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
                ONOFF = true;
                // main.lua 初期化
                File.WriteAllText(targetPath, "");
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
            // MessageBox.Show("Dispose");
        }

        /// <summary>
        /// メッセージを受け取ったら書き出すまでためておく
        /// </summary>
        public void OnMessageReceived(ISiteMessage message, IMessageMetadata messageMetadata)
        {
            if (ONOFF) // 稼働中
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

                // 追加
                buffEmit.Add("    cytanb.EmitCommentMessage(\'" + _comment + "\', {name = \'" + _name + "\', commentSource = \'" + _cmntSource + "\'})");
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
                s1 = "local cytanb = cytanb or require(\'cytanb\')(_ENV)\n\nif vci.assets.IsMine then\n";

                // 接続時は最新データーを１件
                // それ以外はたまっていたもの全部
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

                s3 = "\nend\n"; // 念のため最後に \n を入れておく Visual Studio Code で警告が出る場合がある　なくても正常に動作はする

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

            // SHOWROOM：デフォルト対応 コメントの転送必要なし
            // ニコ生：運営コメが取得ができない？
            // case NicoMessageType.Comment: OK
            // case NicoMessageType.Item Ad Info Unknown Kick Ignored 反応なし
            //
            // Connected Disconnected 反応しているか不明　一応残しておく
            // 未検証・未確認の部分あり
            //
            if (false) { }
            // Youtube Live
            else if (message is IYouTubeLiveMessage youTubeLiveMessage)
            {
                cmntSource = "Youtubelive";
                switch (youTubeLiveMessage.YouTubeLiveMessageType)
                {
                    case YouTubeLiveMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (youTubeLiveMessage as IYouTubeLiveConnected).Text;
                        break;
                    case YouTubeLiveMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (youTubeLiveMessage as IYouTubeLiveDisconnected).Text;
                        break;
                    case YouTubeLiveMessageType.Comment:
                        name = (youTubeLiveMessage as IYouTubeLiveComment).NameItems.ToText();
                        // comment = (youTubeLiveMessage as IYouTubeLiveComment).CommentItems.ToText();
                        comment = (youTubeLiveMessage as IYouTubeLiveComment).CommentItems.ToTextWithImageAlt();
                        break;
                    case YouTubeLiveMessageType.Superchat:
                        // 縦書きコメビュで強調できるように cmntSource を変えておく
                        cmntSource = "YoutubeliveSC";
                        name = (youTubeLiveMessage as IYouTubeLiveSuperchat).NameItems.ToText();
                        // comment = (youTubeLiveMessage as IYouTubeLiveSuperchat).CommentItems.ToText();
                        comment = (youTubeLiveMessage as IYouTubeLiveSuperchat).CommentItems.ToTextWithImageAlt();
                        break;
                }
            }
            // TwitCasting
            else if (message is ITwicasMessage twicasMessage)
            {
                cmntSource = "Twitcasting";
                switch (twicasMessage.TwicasMessageType)
                {
                    case TwicasMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (twicasMessage as ITwicasConnected).Text;
                        break;
                    case TwicasMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
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
            // LineLive
            else if (message is ILineLiveMessage lineLiveMessage)
            {
                cmntSource = "Linelive";
                switch (lineLiveMessage.LineLiveMessageType)
                {
                    case LineLiveMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (lineLiveMessage as ILineLiveConnected).Text;
                        break;
                    case LineLiveMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (lineLiveMessage as ILineLiveDisconnected).Text;
                        break;
                    case LineLiveMessageType.Comment:
                        name = (lineLiveMessage as ILineLiveComment).DisplayName;
                        comment = (lineLiveMessage as ILineLiveComment).Text;
                        break;
                }
            }
            // Mildom
            else if (message is IMildomMessage MildomMessage)
            {
                cmntSource = "Mildom";
                switch (MildomMessage.MildomMessageType)
                {
                    case MildomMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (MildomMessage as IMildomConnected).Text;
                        break;
                    case MildomMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (MildomMessage as IMildomDisconnected).Text;
                        break;
                    case MildomMessageType.Comment:
                        name = (MildomMessage as IMildomComment).UserName;
                        comment = (MildomMessage as IMildomComment).CommentItems.ToText();
                        break;
                    case MildomMessageType.JoinRoom:
                        // name = null;
                        name = "（運営）";
                        comment = (MildomMessage as IMildomJoinRoom).CommentItems.ToText();
                        break;
                        //case MildomMessageType.Leave:
                        //        name = null;
                        //        comment = (MildomMessage as IMildomLeave).CommentItems.ToText();
                        //    break;
                }
            }
            // Mirrativ
            else if (message is IMirrativMessage mirrativMessage)
            {
                cmntSource = "Mirrativ";
                switch (mirrativMessage.MirrativMessageType)
                {
                    case MirrativMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (mirrativMessage as IMirrativConnected).Text;
                        break;
                    case MirrativMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (mirrativMessage as IMirrativDisconnected).Text;
                        break;
                    case MirrativMessageType.Comment:
                        name = (mirrativMessage as IMirrativComment).UserName;
                        comment = (mirrativMessage as IMirrativComment).Text;
                        break;
                    case MirrativMessageType.JoinRoom:
                        // name = null;
                        name = "（運営）";
                        comment = (mirrativMessage as IMirrativJoinRoom).Text;
                        break;
                    case MirrativMessageType.Item:
                        // name = null;
                        name = "（運営）";
                        comment = (mirrativMessage as IMirrativItem).Text;
                        break;
                }
            }
            // Mixer
            else if (message is IMixerMessage MixerMessage)
            {
                cmntSource = "Mixer";
                switch (MixerMessage.MixerMessageType)
                {
                    case MixerMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (MixerMessage as IMixerConnected).Text;
                        break;
                    case MixerMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (MixerMessage as IMixerDisconnected).Text;
                        break;
                    case MixerMessageType.Comment:
                        name = (MixerMessage as IMixerComment).UserName;
                        comment = (MixerMessage as IMixerComment).CommentItems.ToText();
                        break;
                        //case MixerMessageType.Join:
                        //        name = null;
                        //        comment = (MixerMessage as IMixerJoin).CommentItems.ToText();
                        //    break;
                        //case MixerMessageType.Leave:
                        //        name = null;
                        //        comment = (MixerMessage as IMixerLeave).CommentItems.ToText();
                        //    break;
                }
            }
            // Openrec
            else if (message is IOpenrecMessage openrecMessage)
            {
                cmntSource = "Openrec";
                switch (openrecMessage.OpenrecMessageType)
                {
                    case OpenrecMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (openrecMessage as IOpenrecConnected).Text;
                        break;
                    case OpenrecMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (openrecMessage as IOpenrecDisconnected).Text;
                        break;
                    case OpenrecMessageType.Comment:
                        name = (openrecMessage as IOpenrecComment).NameItems.ToText();
                        comment = (openrecMessage as IOpenrecComment).MessageItems.ToText();
                        break;
                }
            }
            // Periscope
            else if (message is IPeriscopeMessage PeriscopeMessage)
            {
                cmntSource = "Periscope";
                switch (PeriscopeMessage.PeriscopeMessageType)
                {
                    case PeriscopeMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (PeriscopeMessage as IPeriscopeConnected).Text;
                        break;
                    case PeriscopeMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (PeriscopeMessage as IPeriscopeDisconnected).Text;
                        break;
                    case PeriscopeMessageType.Comment:
                        name = (PeriscopeMessage as IPeriscopeComment).DisplayName;
                        comment = (PeriscopeMessage as IPeriscopeComment).Text;
                        break;
                    case PeriscopeMessageType.Join:
                        // name = null;
                        name = "（運営）";
                        comment = (PeriscopeMessage as IPeriscopeJoin).Text;
                        break;
                    case PeriscopeMessageType.Leave:
                        // name = null;
                        name = "（運営）";
                        comment = (PeriscopeMessage as IPeriscopeLeave).Text;
                        break;
                }
            }
            // Twitch
            else if (message is ITwitchMessage twitchMessage)
            {
                cmntSource = "Twitch";
                switch (twitchMessage.TwitchMessageType)
                {
                    case TwitchMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (twitchMessage as ITwitchConnected).Text;
                        break;
                    case TwitchMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
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
                cmntSource = "Whowatch";
                switch (whowatchMessage.WhowatchMessageType)
                {
                    case WhowatchMessageType.Connected:
                        // name = null;
                        name = "（運営）";
                        comment = (whowatchMessage as IWhowatchConnected).Text;
                        break;
                    case WhowatchMessageType.Disconnected:
                        // name = null;
                        name = "（運営）";
                        comment = (whowatchMessage as IWhowatchDisconnected).Text;
                        break;
                    case WhowatchMessageType.Comment:
                        name = (whowatchMessage as IWhowatchComment).UserName;
                        comment = (whowatchMessage as IWhowatchComment).Comment;
                        break;
                    case WhowatchMessageType.Item:
                        name = (whowatchMessage as IWhowatchItem).UserName;
                        comment = (whowatchMessage as IWhowatchItem).Comment;
                        break;
                }
            }

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
            // 値を返す
            int returnInt;
            // カレントディレクトリ（MCV 実行ディレクトリ）
            string curDirectory = System.Environment.CurrentDirectory;
            // プラグインディレクトリ
            curDirectory = curDirectory + "\\plugins\\MtoV";
            // 設定ファイル名
            string readPath = curDirectory + "\\MtoV.txt";
            // CommentBaton のディレクトリ用
            string targetDirectory;
            // main.lua
            string targetName;
            targetName = "\\main.lua";

            // ファイルの存在確認
            if (File.Exists(readPath)) // 設定ファイルあり
            {
                // ディレクトリ確認
                targetDirectory = File.ReadAllText(readPath);
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
                ONOFF = false;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n設定ファイルがありません。\nThere is no setting file.\n\n1. …\\MultiCommentViewer\\plugins\\MtoV\\MtoV.txt を作成してください。\n   Please create the text file.\n\n2. MtoV.txt に CommentBaton VCI の場所 C:\\Users\\%ユーザー名%\\AppData\\LocalLow\\infiniteloop Co,Ltd\\VirtualCast\\EmbeddedScriptWorkspace\\CommentBaton を書いてください。\n   Please write the CommentBaton VCI directory in the text file.\n\n3. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
            else if (errorNumber == 2)
            {
                // プラグイン停止
                ONOFF = false;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n指定ディレクトリが CommentBaton ではありません。\nThe directory is not CommentBaton.\n\n1. MtoV.txt の内容（ CommentBaton VCI の場所 C:\\Users\\%ユーザー名%\\AppData\\LocalLow\\infiniteloop Co,Ltd\\VirtualCast\\EmbeddedScriptWorkspace\\CommentBaton ）を確認してください。\n   Please check the CommentBaton directory in the MtoV.txt.\n\n2. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
            else if (errorNumber == 3)
            {
                // プラグイン停止
                ONOFF = false;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n指定ディレクトリがありません。\nThe directory does not Exist.\n\n1. MtoV.txt の内容（ CommentBaton VCI の場所 C:\\Users\\%ユーザー名%\\AppData\\LocalLow\\infiniteloop Co,Ltd\\VirtualCast\\EmbeddedScriptWorkspace\\CommentBaton ）と実在を確認してください。\n   Please check the CommentBaton directory in the MtoV.txt and existence.\n\n2. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
        }
    }
}
