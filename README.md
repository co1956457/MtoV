<pre>
これはマルチコメントビューアのプラグインです。
マルチコメントビューアで受け取ったコメントを CommentBaton VCI に送ります。
（対応：ニコ生、ショールーム、Youtubeライブ(Yt)、ツイキャス(Tc)、Twitch(Tw)、ふわっち(Wh)、OPENREC(OR)）

これにより、縦書きコメビュに ニコ生の運営コメントやYoutubeライブ等のコメントが表示されるようになります。

また、転送モード２を選ぶと、ルームでニコ生やショールームのコメントも縦書きコメビュで見られるようになります。

転送モード１（スタジオ）
ニコ生　　　：運営コメントのみ転送
ショールーム：転送しない
他(Yt,Tc,Tw,Wh,OR)：全コメント転送

転送モード２（ルーム）
ニコ生　　　：全コメント転送
ショールーム：全コメント転送
他(Yt,Tc,Tw,Wh,OR)：全コメント転送


マルチコメントビューアはこちらから入手できます。
https://ryu-s.github.io/app/multicommentviewer

CommentBaton と縦書きコメビュはこちらから入手できます。
https://seed.online/users/100215#products


連携手順
１．バーチャルキャストを立ち上げて CommentBaton と縦書きコメビュを出してください。

２．マルチコメントビューアの plugins フォルダに MtoV フォルダを作成して MtoVPlugin.dll を置いてください。
https://github.com/co1956457/MtoV/releases/
C:\…\MultiCommentViewer\plugins\MtoV\MtoVPlugin.dll
※右クリック→プロパティ→セキュリティ:このファイルは…☑許可する(K)

３．テキストファイルを作成してください。
C:\…\MultiCommentViewer\plugins\MtoV\MtoV.txt

４．MtoV.txt に CommentBaton VCI の場所を書いてください。
C:\Users\%ユーザー名%\AppData\LocalLow\infiniteloop Co,Ltd\VirtualCast\EmbeddedScriptWorkspace\CommentBaton

５．マルチコメントビューアを立ち上げてください。

※既知の問題
　CommentBaton を先に出現させた時に、前回最後のコメントが main.lua に残っていたらそれが流れます。
　既存コメントが大量にある状態では、接続時に過去のコメントが流れることがあります。
　負荷が高くなると（例：1秒間に10コメント等）、表示までに時間がかかることがあります。
　また、一部のコメントが反映されないこともあります。

ライセンス： GPL-3.0 (参考にした BouyomiPlugin が GPL-3.0)


This is MultiCommentViewer plugin.
This plugin sends the comments to CommentBaton VCI.
(Available: NicoLive, SHOWROOM, YoutubeLive(Yt), TwitCasting(Tc), Twitch(Tw), Whowatch(Wh), OPENREC(OR))

Then, Vertical Comment Viewer VCI would be able to show the Nicolive special comments or Youtube live comments.
If you choose the transfer mode 2, you can read all Nicolive comments or SHOWROOM coments in the "Room".

Transfer mode 1 (Studio)
Nicolive: only special comments
SHOWROOM: N/A
Other(Yt,Tc,Tw,Wh,OR): all comments

Transfer mode 2 (Room)
Nicolive: all comments
SHOWROOM: all comments
Other(Yt,Tc,Tw,Wh,OR): all comments


You can get Multicommentviewer from this page.
https://ryu-s.github.io/app/multicommentviewer
You can get CommentBaton and Vertical Comment Viewer from this page.
https://seed.online/users/100215#products

How to use this dll
1. Please boot the VirtualCast, then cause to appear CommentBaton and Vertical Comment Viewer VCI.

2. Please create MtoV folder under the plugins folder and put this MtoVPlugin.dll in the folder.
https://github.com/co1956457/MtoV/releases/
C:\…\MultiCommentViewer\plugins\MtoV\MtoVPlugin.dll
! Right click -> Properties -> please tick the checkbox named "Unblock".

3. Please create the text file.
C:\…\MultiCommentViewer\plugins\MtoV\MtoV.txt

4. Please write the CommentBaton VCI directory in the MtoV.txt.
C:\Users\%UserName%\AppData\LocalLow\infiniteloop Co,Ltd\VirtualCast\EmbeddedScriptWorkspace\CommentBaton

5. Please boot MultiCommentViewer.

! Known Issues
 When CommentBaton VCI was appeared, the VCI sends the last comment if it was in main.lua.
 If there were a lot of comments, the VCI might sends a past comment.
 High loading situation, for example 10 comments per 1 seconds, it would take a time to show comments.
 Some comments might be lost.

License: GPL-3.0 (Because, BouyomiPlugin which I referred to the cods is GPL-3.0)
</pre>
