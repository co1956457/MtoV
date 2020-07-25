// https://higehito.qrunch.io/entries/2eqRPvFL8sWpKIR3              この記事を参考に基本的な参照等追加                   webの公開情報
// https://github.com/CommentViewerCollection/MultiCommentViewer    BouyomiPlugin を参考に作成                           GPL-3.0 License 
// https://github.com/oocytanb                                      CommentBaton から縦書きコメビュにメッセージを送る    MIT License
//
// SPDX-License-Identifier: GPL-3.0   // MultiCommentViewer の BouyomiPlugin が GPL-3.0 なので。
// 20200725 v1.0 Taki co1956457
//               cytanb ver. Commits on Jul 24, 2020.
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
                // cytanb ver.Commits on Jul 15, 2020
                // \ -> \\      ' -> \'     " -> \"
                s1 = "-- SPDX-License-Identifier: MIT\n-- Copyright (c) 2019 oO (https://github.com/oocytanb)\n---@type cytanb @See `cytanb_annotations.lua`\n\nlocal cytanb=(function()local b=\'__CYTANB_INSTANCE_ID\'local c;local d;local e;local f;local g;local h;local i;local j;local k=false;local l;local m;local n;local a;local o=function(p,q)local r=p+q-1;return p,r,r+1 end;local s=function(p,q)local r=p-q+1;return r,p,r-1 end;local t=function(u,v,w,p,x)local y=w.searchMap;for z,q in pairs(w.lengthList)do if q<=0 then error(\'SearchPattern: Invalid parameter: searchLen <= 0\')else local A,B,r=x(p,q)if A>=1 and B<=v then local C=string.sub(u,A,B)if y[C]then return true,q,r end end end end;return false,-1,-1 end;local D=function(u,w,p,x)if u==nil or w==nil then return false,-1 end;if w.hasEmptySearch then return true,0 end;local v=string.len(u)local E=w.repeatMin;local F=w.repeatMax;local G=F<0;local H=p;local I=0;local J=0;while G or J<F do local K,L,r=t(u,v,w,H,x)if K then if L<=0 then error(\'SearchPattern: Invalid parameter\')end;H=r;I=I+L;J=J+1 else break end end;if J>=E then return true,I else return false,-1 end end;local M=function(u,N,p,O,x,P)if u==nil or N==nil then return false,-1 end;local q=string.len(N)if O then local K=P(u,N)return K,K and q or-1 else if q==0 then return true,q end;local v=string.len(u)local A,B=x(p,q)if A>=1 and B<=v then local C=string.sub(u,A,B)local K=P(C,N)return K,K and q or-1 else return false,-1 end end end;local Q=function(R,S)for T=1,4 do local U=R[T]-S[T]if U~=0 then return U end end;return 0 end;local V;V={__eq=function(R,S)return R[1]==S[1]and R[2]==S[2]and R[3]==S[3]and R[4]==S[4]end,__lt=function(R,S)return Q(R,S)<0 end,__le=function(R,S)return Q(R,S)<=0 end,__tostring=function(W)local X=W[2]or 0;local Y=W[3]or 0;return string.format(\'%08x-%04x-%04x-%04x-%04x%08x\',bit32.band(W[1]or 0,0xFFFFFFFF),bit32.band(bit32.rshift(X,16),0xFFFF),bit32.band(X,0xFFFF),bit32.band(bit32.rshift(Y,16),0xFFFF),bit32.band(Y,0xFFFF),bit32.band(W[4]or 0,0xFFFFFFFF))end,__concat=function(R,S)local Z=getmetatable(R)local _=Z==V or type(Z)==\'table\'and Z.__concat==V.__concat;local a0=getmetatable(S)local a1=a0==V or type(a0)==\'table\'and a0.__concat==V.__concat;if not _ and not a1 then error(\'UUID: attempt to concatenate illegal values\',2)end;return(_ and V.__tostring(R)or R)..(a1 and V.__tostring(S)or S)end}local a2=\'__CYTANB_CONST_VARIABLES\'local a3=function(table,a4)local a5=getmetatable(table)if a5 then local a6=rawget(a5,a2)if a6 then local a7=rawget(a6,a4)if type(a7)==\'function\'then return a7(table,a4)else return a7 end end end;return nil end;local a8=function(table,a4,a9)local a5=getmetatable(table)if a5 then local a6=rawget(a5,a2)if a6 then if rawget(a6,a4)~=nil then error(\'Cannot assign to read only field \"\'..a4 ..\'\"\',2)end end end;rawset(table,a4,a9)end;local aa=function(ab,ac)local ad=ab[a.TypeParameterName]if a.NillableHasValue(ad)and a.NillableValue(ad)~=ac then return false,false end;return a.NillableIfHasValueOrElse(h[ac],function(ae)local af=ae.compositionFieldNames;local ag=ae.compositionFieldLength;local ah=false;for ai,a9 in pairs(ab)do if af[ai]then ag=ag-1;if ag<=0 and ah then break end elseif ai~=a.TypeParameterName then ah=true;if ag<=0 then break end end end;return ag<=0,ah end,function()return false,false end)end;local aj=function(u)return a.StringReplace(a.StringReplace(u,a.EscapeSequenceTag,a.EscapeSequenceTag..a.EscapeSequenceTag),\'/\',a.SolidusTag)end;local ak=function(u,al)local am=string.len(u)local an=string.len(a.EscapeSequenceTag)if an>am then return u end;local ao=\'\'local T=1;while T<am do local B,ap=string.find(u,a.EscapeSequenceTag,T,true)if not B then if T==1 then ao=u else ao=ao..string.sub(u,T)end;break end;if B>T then ao=ao..string.sub(u,T,B-1)end;local aq=false;for ar,as in ipairs(g)do local K=a.StringStartsWith(u,as.search,B)if K then ao=ao..(al and al(as.tag)or as.replacement)T=B+string.len(as.search)aq=true;break end end;if not aq then ao=ao..a.EscapeSequenceTag;T=ap+1 end end;return ao end;local at;at=function(au,av)if type(au)~=\'table\'then return au end;if not av then av={}end;if av[au]then error(\'circular reference\')end;av[au]=true;local aw={}for ai,a9 in pairs(au)do local ax=type(ai)local ay;if ax==\'string\'then ay=aj(ai)elseif ax==\'number\'then ay=tostring(ai)..a.ArrayNumberTag else ay=ai end;local az=type(a9)if az==\'string\'then aw[ay]=aj(a9)elseif az==\'number\'and a9<0 then aw[tostring(ay)..a.NegativeNumberTag]=tostring(a9)else aw[ay]=at(a9,av)end end;av[au]=nil;return aw end;local aA;aA=function(aw,aB)if type(aw)~=\'table\'then return aw end;local au={}for ai,a9 in pairs(aw)do local ay;local aC=false;if type(ai)==\'string\'then local aD=false;ay=ak(ai,function(aE)if aE==a.NegativeNumberTag then aC=true elseif aE==a.ArrayNumberTag then aD=true end;return nil end)if aD then ay=tonumber(ay)or ay end else ay=ai;aC=false end;if aC and type(a9)==\'string\'then au[ay]=tonumber(a9)elseif type(a9)==\'string\'then au[ay]=ak(a9,function(aE)return i[aE]end)else au[ay]=aA(a9,aB)end end;if not aB then a.NillableIfHasValue(au[a.TypeParameterName],function(aF)a.NillableIfHasValue(h[aF],function(ae)local aG,ah=ae.fromTableFunc(au)if not ah then a.NillableIfHasValue(aG,function(W)au=W end)end end)end)end;return au end;local aH={[\'nil\']=function(aI)return nil end,[\'number\']=function(aI)return tonumber(aI)end,[\'string\']=function(aI)return tostring(aI)end,[\'boolean\']=function(aI)if aI then return true else return false end end}local aJ=function(aI,aK)local aL=type(aI)if aL==aK then return aI else local aM=aH[aK]if aM then return aM(aI)else return nil end end end;local aN=function(aO,aP)if aP and type(aP)==\'table\'then local aQ={}for a4,aI in pairs(aO)do local aR=aP[a4]local aS;if aR==nil then aS=aI else local aT=aJ(aR,type(aI))if aT==nil then aS=aI else aS=aT end end;aQ[a4]=aS end;aQ[a.MessageOriginalSender]=aO;return aQ else return aO end end;local aU=function(aV,aW,aP,aX)local aY={type=aX,name=\'\',commentSource=\'\'}local aZ={[a.MessageValueParameterName]=tostring(aW),[a.MessageSenderOverride]=type(aP)==\'table\'and a.Extend(aY,aP,true)or aY}a.EmitMessage(aV,aZ)end;local a_=function(aV,b0,b1)local b2,b3=(function()local aM=function(aO,b4,aZ)local aW=tostring(aZ[a.MessageValueParameterName]or\'\')b1(aO,b0,aW)end;local b2=a.OnMessage(aV,aM)local b3=a.OnMessage(b0,aM)return b2,b3 end)()return{Off=function()b2.Off()b3.Off()end}end;a={InstanceID=function()if m==\'\'then m=vci.state.Get(b)or\'\'end;return m end,NillableHasValue=function(b5)return b5~=nil end,NillableValue=function(b5)if b5==nil then error(\'nillable: value is nil\',2)end;return b5 end,NillableValueOrDefault=function(b5,b6)if b5==nil then if b6==nil then error(\'nillable: defaultValue is nil\',2)end;return b6 else return b5 end end,NillableIfHasValue=function(b5,b1)if b5==nil then return nil else return b1(b5)end end,NillableIfHasValueOrElse=function(b5,b1,b7)if b5==nil then return b7()else return b1(b5)end end,MakeSearchPattern=function(b8,b9,ba)local E=b9 and math.floor(b9)or 1;if E<0 then error(\'SearchPattern: Invalid parameter: optRepeatMin < 0\')end;local F=ba and math.floor(ba)or E;if F>=0 and F<E then error(\'SearchPattern: Invalid parameter: repeatMax < repeatMin\')end;local bb=F==0;local y={}local bc={}local bd={}local be=0;for bf,bg in pairs(b8)do local q=string.len(bg)if q==0 then bb=true else y[bg]=q;if not bc[q]then bc[q]=true;be=be+1;bd[be]=q end end end;table.sort(bd,function(bh,K)return bh>K end)return{hasEmptySearch=bb,searchMap=y,lengthList=bd,repeatMin=E,repeatMax=F}end,StringStartsWith=function(u,N,bi)local H=bi and math.max(1,math.floor(bi))or 1;if type(N)==\'table\'then return D(u,N,H,o)else return M(u,N,H,H==1,o,string.startsWith)end end,StringEndsWith=function(u,N,bj)if u==nil then return false,-1 end;local v=string.len(u)local H=bj and math.min(v,math.floor(bj))or v;if type(N)==\'table\'then return D(u,N,H,s)else return M(u,N,H,H==v,s,string.endsWith)end end,StringTrimStart=function(u,bk)if u==nil or u==\'\'then return u end;local K,L=a.StringStartsWith(u,bk or c)if K and L>=1 then return string.sub(u,L+1)else return u end end,StringTrimEnd=function(u,bk)if u==nil or u==\'\'then return u end;local K,L=a.StringEndsWith(u,bk or c)if K and L>=1 then return string.sub(u,1,string.len(u)-L)else return u end end,StringTrim=function(u,bk)return a.StringTrimEnd(a.StringTrimStart(u,bk),bk)end,StringReplace=function(u,bl,bm)local bn;local am=string.len(u)if bl==\'\'then bn=bm;for T=1,am do bn=bn..string.sub(u,T,T)..bm end else bn=\'\'local T=1;while true do local A,B=string.find(u,bl,T,true)if A then bn=bn..string.sub(u,T,A-1)..bm;T=B+1;if T>am then break end else bn=T==1 and u or bn..string.sub(u,T)break end end end;return bn end,SetConst=function(bl,aV,W)if type(bl)~=\'table\'then error(\'Cannot set const to non-table target\',2)end;local bo=getmetatable(bl)local a5=bo or{}local bp=rawget(a5,a2)if rawget(bl,aV)~=nil then error(\'Non-const field \"\'..aV..\'\" already exists\',2)end;if not bp then bp={}rawset(a5,a2,bp)a5.__index=a3;a5.__newindex=a8 end;rawset(bp,aV,W)if not bo then setmetatable(bl,a5)end;return bl end,\nSetConstEach=function(bl,bq)for ai,a9 in pairs(bq)do a.SetConst(bl,ai,a9)end;return bl end,Extend=function(bl,br,bs,bt,av)if bl==br or type(bl)~=\'table\'or type(br)~=\'table\'then return bl end;if bs then if not av then av={}end;if av[br]then error(\'circular reference\')end;av[br]=true end;for ai,a9 in pairs(br)do if bs and type(a9)==\'table\'then local bu=bl[ai]bl[ai]=a.Extend(type(bu)==\'table\'and bu or{},a9,bs,bt,av)else bl[ai]=a9 end end;if not bt then local bv=getmetatable(br)if type(bv)==\'table\'then if bs then local bw=getmetatable(bl)setmetatable(bl,a.Extend(type(bw)==\'table\'and bw or{},bv,true))else setmetatable(bl,bv)end end end;if bs then av[br]=nil end;return bl end,Vars=function(a9,bx,by,av)local bz;if bx then bz=bx~=\'__NOLF\'else bx=\'  \'bz=true end;if not by then by=\'\'end;if not av then av={}end;local bA=type(a9)if bA==\'table\'then av[a9]=av[a9]and av[a9]+1 or 1;local bB=bz and by..bx or\'\'local u=\'(\'..tostring(a9)..\') {\'local bC=true;for a4,aI in pairs(a9)do if bC then bC=false else u=u..(bz and\',\'or\', \')end;if bz then u=u..\'\\n\'..bB end;if type(aI)==\'table\'and av[aI]and av[aI]>0 then u=u..a4 ..\' = (\'..tostring(aI)..\')\'else u=u..a4 ..\' = \'..a.Vars(aI,bx,bB,av)end end;if not bC and bz then u=u..\'\\n\'..by end;u=u..\'}\'av[a9]=av[a9]-1;if av[a9]<=0 then av[a9]=nil end;return u elseif bA==\'function\'or bA==\'thread\'or bA==\'userdata\'then return\'(\'..bA..\')\'elseif bA==\'string\'then return\'(\'..bA..\') \'..string.format(\'%q\',a9)else return\'(\'..bA..\') \'..tostring(a9)end end,GetLogLevel=function()return j end,SetLogLevel=function(bD)j=bD end,IsOutputLogLevelEnabled=function()return k end,SetOutputLogLevelEnabled=function(bE)k=not not bE end,Log=function(bD,...)if bD<=j then local bF=k and(l[bD]or\'LOG LEVEL \'..tostring(bD))..\' | \'or\'\'local bG=table.pack(...)if bG.n==1 then local a9=bG[1]if a9~=nil then local u=type(a9)==\'table\'and a.Vars(a9)or tostring(a9)print(k and bF..u or u)else print(bF)end else local u=bF;for T=1,bG.n do local a9=bG[T]if a9~=nil then u=u..(type(a9)==\'table\'and a.Vars(a9)or tostring(a9))end end;print(u)end end end,LogFatal=function(...)a.Log(a.LogLevelFatal,...)end,LogError=function(...)a.Log(a.LogLevelError,...)end,LogWarn=function(...)a.Log(a.LogLevelWarn,...)end,LogInfo=function(...)a.Log(a.LogLevelInfo,...)end,LogDebug=function(...)a.Log(a.LogLevelDebug,...)end,LogTrace=function(...)a.Log(a.LogLevelTrace,...)end,FatalLog=function(...)a.LogFatal(...)end,ErrorLog=function(...)a.LogError(...)end,WarnLog=function(...)a.LogWarn(...)end,InfoLog=function(...)a.LogInfo(...)end,DebugLog=function(...)a.LogDebug(...)end,TraceLog=function(...)a.LogTrace(...)end,ListToMap=function(bH,bI)local bJ={}if bI==nil then for ai,a9 in pairs(bH)do bJ[a9]=a9 end elseif type(bI)==\'function\'then for ai,a9 in pairs(bH)do local bK,bL=bI(a9)bJ[bK]=bL end else for ai,a9 in pairs(bH)do bJ[a9]=bI end end;return bJ end,Round=function(bM,bN)if bN then local bO=10^bN;return math.floor(bM*bO+0.5)/bO else return math.floor(bM+0.5)end end,Clamp=function(W,bP,bQ)return math.max(bP,math.min(W,bQ))end,Lerp=function(bh,K,bA)if bA<=0.0 then return bh elseif bA>=1.0 then return K else return bh+(K-bh)*bA end end,LerpUnclamped=function(bh,K,bA)if bA==0.0 then return bh elseif bA==1.0 then return K else return bh+(K-bh)*bA end end,PingPong=function(bA,bR)if bR==0 then return 0,1 end;local bS=math.floor(bA/bR)local bT=bA-bS*bR;if bS<0 then if(bS+1)%2==0 then return bR-bT,-1 else return bT,1 end else if bS%2==0 then return bT,1 else return bR-bT,-1 end end end,VectorApproximatelyEquals=function(bU,bV)return(bU-bV).sqrMagnitude<1E-10 end,QuaternionApproximatelyEquals=function(bU,bV)local bW=Quaternion.Dot(bU,bV)return bW<1.0+1E-06 and bW>1.0-1E-06 end,QuaternionToAngleAxis=function(bX)local bS=bX.normalized;local bY=math.acos(bS.w)local bZ=math.sin(bY)local b_=math.deg(bY*2.0)local c0;if math.abs(bZ)<=Quaternion.kEpsilon then c0=Vector3.right else local A=1.0/bZ;c0=Vector3.__new(bS.x*A,bS.y*A,bS.z*A)end;return b_,c0 end,QuaternionTwist=function(bX,c1)if c1.sqrMagnitude<Vector3.kEpsilonNormalSqrt then return Quaternion.identity end;local c2=Vector3.__new(bX.x,bX.y,bX.z)if c2.sqrMagnitude>=Vector3.kEpsilonNormalSqrt then local c3=Vector3.Project(c2,c1)if c3.sqrMagnitude>=Vector3.kEpsilonNormalSqrt then local c4=Quaternion.__new(c3.x,c3.y,c3.z,bX.w)c4.Normalize()return c4 else return Quaternion.AngleAxis(0,c1)end else local c5=a.QuaternionToAngleAxis(bX)return Quaternion.AngleAxis(c5,c1)end end,ApplyQuaternionToVector3=function(bX,c6)local c7=bX.w*c6.x+bX.y*c6.z-bX.z*c6.y;local c8=bX.w*c6.y-bX.x*c6.z+bX.z*c6.x;local c9=bX.w*c6.z+bX.x*c6.y-bX.y*c6.x;local ca=-bX.x*c6.x-bX.y*c6.y-bX.z*c6.z;return Vector3.__new(ca*-bX.x+c7*bX.w+c8*-bX.z-c9*-bX.y,ca*-bX.y-c7*-bX.z+c8*bX.w+c9*-bX.x,ca*-bX.z+c7*-bX.y-c8*-bX.x+c9*bX.w)end,RotateAround=function(cb,cc,cd,ce)return cd+ce*(cb-cd),ce*cc end,Random32=function()return bit32.band(math.random(-2147483648,2147483646),0xFFFFFFFF)end,RandomUUID=function()return a.UUIDFromNumbers(a.Random32(),bit32.bor(0x4000,bit32.band(a.Random32(),0xFFFF0FFF)),bit32.bor(0x80000000,bit32.band(a.Random32(),0x3FFFFFFF)),a.Random32())end,UUIDString=function(cf)return V.__tostring(cf)end,UUIDFromNumbers=function(...)local cg=...local bA=type(cg)local ch,ci,cj,ck;if bA==\'table\'then ch=cg[1]ci=cg[2]cj=cg[3]ck=cg[4]else ch,ci,cj,ck=...end;local cf={bit32.band(ch or 0,0xFFFFFFFF),bit32.band(ci or 0,0xFFFFFFFF),bit32.band(cj or 0,0xFFFFFFFF),bit32.band(ck or 0,0xFFFFFFFF)}setmetatable(cf,V)return cf end,UUIDFromString=function(u)local am=string.len(u)if am==32 then local cf=a.UUIDFromNumbers(0,0,0,0)for T=1,4 do local A=1+(T-1)*8;if not a.StringStartsWith(u,e,A)then return nil end;cf[T]=tonumber(string.sub(u,A,A+7),16)end;return cf elseif am==36 then if not a.StringStartsWith(u,e,1)then return nil end;local ch=tonumber(string.sub(u,1,8),16)if not a.StringStartsWith(u,\'-\',9)or not a.StringStartsWith(u,d,10)or not a.StringStartsWith(u,\'-\',14)or not a.StringStartsWith(u,d,15)then return nil end;local ci=tonumber(string.sub(u,10,13)..string.sub(u,15,18),16)if not a.StringStartsWith(u,\'-\',19)or not a.StringStartsWith(u,d,20)or not a.StringStartsWith(u,\'-\',24)or not a.StringStartsWith(u,d,25)then return nil end;local cj=tonumber(string.sub(u,20,23)..string.sub(u,25,28),16)if not a.StringStartsWith(u,e,29)then return nil end;local ck=tonumber(string.sub(u,29),16)return a.UUIDFromNumbers(ch,ci,cj,ck)else return nil end end,ParseUUID=function(u)return a.UUIDFromString(u)end,CreateCircularQueue=function(cl)if type(cl)~=\'number\'or cl<1 then error(\'CreateCircularQueue: Invalid argument: capacity = \'..tostring(cl),2)end;local self;local cm=math.floor(cl)local ao={}local cn=0;local co=0;local cp=0;self={Size=function()return cp end,Clear=function()cn=0;co=0;cp=0 end,IsEmpty=function()return cp==0 end,Offer=function(cq)ao[cn+1]=cq;cn=(cn+1)%cm;if cp<cm then cp=cp+1 else co=(co+1)%cm end;return true end,OfferFirst=function(cq)co=(cm+co-1)%cm;ao[co+1]=cq;if cp<cm then cp=cp+1 else cn=(cm+cn-1)%cm end;return true end,Poll=function()if cp==0 then return nil else local cq=ao[co+1]co=(co+1)%cm;cp=cp-1;return cq end end,PollLast=function()if cp==0 then return nil else cn=(cm+cn-1)%cm;local cq=ao[cn+1]cp=cp-1;return cq end end,Peek=function()if cp==0 then return nil else return ao[co+1]end end,PeekLast=function()if cp==0 then return nil else return ao[(cm+cn-1)%cm+1]end end,Get=function(cr)if cr<1 or cr>cp then a.LogError(\'CreateCircularQueue.Get: index is outside the range: \'..cr)return nil end;return ao[(co+cr-1)%cm+1]end,IsFull=function()return cp>=cm end,MaxSize=function()return cm end}return self end,DetectClicks=function(cs,ct,cu)local cv=cs or 0;local cw=cu or TimeSpan.FromMilliseconds(500)local cx=vci.me.Time;local cy=ct and cx>ct+cw and 1 or cv+1;return cy,cx end,ColorRGBToHSV=function(cz)local bT=math.max(0.0,math.min(cz.r,1.0))local cA=math.max(0.0,math.min(cz.g,1.0))local K=math.max(0.0,math.min(cz.b,1.0))local bQ=math.max(bT,cA,K)local bP=math.min(bT,cA,K)local cB=bQ-bP;local a7;if cB==0.0 then a7=0.0 elseif bQ==bT then a7=(cA-K)/cB/6.0 elseif bQ==cA then a7=(2.0+(K-bT)/cB)/6.0 else a7=(4.0+(bT-cA)/cB)/6.0 end;if a7<0.0 then a7=a7+1.0 end;local cC=bQ==0.0 and cB or cB/bQ;local a9=bQ;return a7,cC,a9 end,ColorFromARGB32=function(cD)local cE=type(cD)==\'number\'and cD or 0xFF000000;return Color.__new(bit32.band(bit32.rshift(cE,16),0xFF)/0xFF,bit32.band(bit32.rshift(cE,8),0xFF)/0xFF,bit32.band(cE,0xFF)/0xFF,bit32.band(bit32.rshift(cE,24),0xFF)/0xFF)end,ColorToARGB32=function(cz)return bit32.bor(bit32.lshift(bit32.band(a.Round(0xFF*cz.a),0xFF),24),bit32.lshift(bit32.band(a.Round(0xFF*cz.r),0xFF),16),bit32.lshift(bit32.band(a.Round(0xFF*cz.g),0xFF),8),bit32.band(a.Round(0xFF*cz.b),0xFF))end,ColorFromIndex=function(cF,cG,cH,cI,cJ)local cK=math.max(math.floor(cG or a.ColorHueSamples),1)local cL=cJ and cK or cK-1;local cM=math.max(math.floor(cH or a.ColorSaturationSamples),1)local cN=math.max(math.floor(cI or a.ColorBrightnessSamples),1)local cr=a.Clamp(math.floor(cF or 0),0,cK*cM*cN-1)local cO=cr%cK;local cP=math.floor(cr/cK)local A=cP%cM;local cQ=math.floor(cP/cM)if cJ or cO~=cL then local a7=cO/cL;local cC=(cM-A)/cM;local a9=(cN-cQ)/cN;return Color.HSVToRGB(a7,cC,a9)else local a9=(cN-cQ)/cN*A/(cM-1)return Color.HSVToRGB(0.0,0.0,a9)end end,\nColorToIndex=function(cz,cG,cH,cI,cJ)local cK=math.max(math.floor(cG or a.ColorHueSamples),1)local cL=cJ and cK or cK-1;local cM=math.max(math.floor(cH or a.ColorSaturationSamples),1)local cN=math.max(math.floor(cI or a.ColorBrightnessSamples),1)local a7,cC,a9=a.ColorRGBToHSV(cz)local A=a.Round(cM*(1.0-cC))if cJ or A<cM then local cR=a.Round(cL*a7)if cR>=cL then cR=0 end;if A>=cM then A=cM-1 end;local cQ=math.min(cN-1,a.Round(cN*(1.0-a9)))return cR+cK*(A+cM*cQ)else local cS=a.Round((cM-1)*a9)if cS==0 then local cT=a.Round(cN*(1.0-a9))if cT>=cN then return cK-1 else return cK*(1+a.Round(a9*(cM-1)/(cN-cT)*cN)+cM*cT)-1 end else return cK*(1+cS+cM*a.Round(cN*(1.0-a9*(cM-1)/cS)))-1 end end end,ColorToTable=function(cz)return{[a.TypeParameterName]=a.ColorTypeName,r=cz.r,g=cz.g,b=cz.b,a=cz.a}end,ColorFromTable=function(ab)local K,ah=aa(ab,a.ColorTypeName)return K and Color.__new(ab.r,ab.g,ab.b,ab.a)or nil,ah end,Vector2ToTable=function(W)return{[a.TypeParameterName]=a.Vector2TypeName,x=W.x,y=W.y}end,Vector2FromTable=function(ab)local K,ah=aa(ab,a.Vector2TypeName)return K and Vector2.__new(ab.x,ab.y)or nil,ah end,Vector3ToTable=function(W)return{[a.TypeParameterName]=a.Vector3TypeName,x=W.x,y=W.y,z=W.z}end,Vector3FromTable=function(ab)local K,ah=aa(ab,a.Vector3TypeName)return K and Vector3.__new(ab.x,ab.y,ab.z)or nil,ah end,Vector4ToTable=function(W)return{[a.TypeParameterName]=a.Vector4TypeName,x=W.x,y=W.y,z=W.z,w=W.w}end,Vector4FromTable=function(ab)local K,ah=aa(ab,a.Vector4TypeName)return K and Vector4.__new(ab.x,ab.y,ab.z,ab.w)or nil,ah end,QuaternionToTable=function(W)return{[a.TypeParameterName]=a.QuaternionTypeName,x=W.x,y=W.y,z=W.z,w=W.w}end,QuaternionFromTable=function(ab)local K,ah=aa(ab,a.QuaternionTypeName)return K and Quaternion.__new(ab.x,ab.y,ab.z,ab.w)or nil,ah end,TableToSerializable=function(au)return at(au)end,TableFromSerializable=function(aw,aB)return aA(aw,aB)end,TableToSerialiable=function(au)return at(au)end,TableFromSerialiable=function(aw,aB)return aA(aw,aB)end,EmitMessage=function(aV,aZ)local aw=a.NillableIfHasValueOrElse(aZ,function(au)if type(au)~=\'table\'then error(\'EmitMessage: Invalid argument: table expected\',3)end;return a.TableToSerializable(au)end,function()return{}end)aw[a.InstanceIDParameterName]=a.InstanceID()vci.message.Emit(aV,json.serialize(aw))end,OnMessage=function(aV,b1)local aM=function(aO,b4,aW)if type(aW)==\'string\'and string.startsWith(aW,\'{\')then local cU,aw=pcall(json.parse,aW)if cU and type(aw)==\'table\'and aw[a.InstanceIDParameterName]then local cV=a.TableFromSerializable(aw)b1(aN(aO,cV[a.MessageSenderOverride]),b4,cV)return end end;b1(aO,b4,{[a.MessageValueParameterName]=aW})end;vci.message.On(aV,aM)return{Off=function()if aM then aM=nil end end}end,OnInstanceMessage=function(aV,b1)local aM=function(aO,b4,aZ)local cW=a.InstanceID()if cW~=\'\'and cW==aZ[a.InstanceIDParameterName]then b1(aO,b4,aZ)end end;return a.OnMessage(aV,aM)end,EmitCommentMessage=function(aW,aP)aU(a.DedicatedCommentMessageName,aW,aP,\'comment\')end,OnCommentMessage=function(b1)a_(a.DedicatedCommentMessageName,\'comment\',b1)end,EmitNotificationMessage=function(aW,aP)aU(a.DedicatedNotificationMessageName,aW,aP,\'notification\')end,OnNotificationMessage=function(b1)a_(a.DedicatedNotificationMessageName,\'notification\',b1)end,GetEffekseerEmitterMap=function(aV)local cX=vci.assets.GetEffekseerEmitters(aV)if not cX then return nil end;local bJ={}for T,cY in pairs(cX)do bJ[cY.EffectName]=cY end;return bJ end,ClientID=function()return n end,ParseTagString=function(u)local cZ=string.find(u,\'#\',1,true)if not cZ then return{},u end;local c_={}local d0=string.sub(u,1,cZ-1)cZ=cZ+1;local am=string.len(u)while cZ<=am do local d1,d2=a.StringStartsWith(u,f,cZ)if d1 then local d3=cZ+d2;local d4=string.sub(u,cZ,d3-1)local d5=d4;cZ=d3;if cZ<=am and a.StringStartsWith(u,\'=\',cZ)then cZ=cZ+1;local d6,d7=a.StringStartsWith(u,f,cZ)if d6 then local d8=cZ+d7;d5=string.sub(u,cZ,d8-1)cZ=d8 else d5=\'\'end end;c_[d4]=d5 end;cZ=string.find(u,\'#\',cZ,true)if not cZ then break end;cZ=cZ+1 end;return c_,d0 end,CalculateSIPrefix=(function()local d9=9;local da={\'y\',\'z\',\'a\',\'f\',\'p\',\'n\',\'u\',\'m\',\'\',\'k\',\'M\',\'G\',\'T\',\'P\',\'E\',\'Z\',\'Y\'}local db=#da;return function(bM)local dc=bM==0 and 0 or a.Clamp(math.floor(math.log(math.abs(bM),1000)),1-d9,db-d9)return dc==0 and bM or bM/1000^dc,da[d9+dc],dc*3 end end)(),CreateLocalSharedProperties=function(dd,de)local df=TimeSpan.FromSeconds(5)local dg=\'33657f0e-7c44-4ee7-acd9-92dd8b8d807a\'local dh=\'__CYTANB_LOCAL_SHARED_PROPERTIES_LISTENER_MAP\'if type(dd)~=\'string\'or string.len(dd)<=0 or type(de)~=\'string\'or string.len(de)<=0 then error(\'LocalSharedProperties: Invalid arguments\',2)end;local di=_G[dg]if not di then di={}_G[dg]=di end;di[de]=vci.me.UnscaledTime;local dj=_G[dd]if not dj then dj={[dh]={}}_G[dd]=dj end;local dk=dj[dh]local self;self={GetLspID=function()return dd end,GetLoadID=function()return de end,GetProperty=function(a4,b6)local W=dj[a4]if W==nil then return b6 else return W end end,SetProperty=function(a4,W)if a4==dh then error(\'LocalSharedProperties: Invalid argument: key = \',a4,2)end;local cx=vci.me.UnscaledTime;local dl=dj[a4]dj[a4]=W;for dm,cW in pairs(dk)do local bA=di[cW]if bA and bA+df>=cx then dm(self,a4,W,dl)else dm(self,a.LOCAL_SHARED_PROPERTY_EXPIRED_KEY,true,false)dk[dm]=nil;di[cW]=nil end end end,Clear=function()for a4,W in pairs(dj)do if a4~=dh then self.SetProperty(a4,nil)end end end,Each=function(b1)for a4,W in pairs(dj)do if a4~=dh and b1(W,a4,self)==false then return false end end end,AddListener=function(dm)dk[dm]=de end,RemoveListener=function(dm)dk[dm]=nil end,UpdateAlive=function()di[de]=vci.me.UnscaledTime end}return self end,EstimateFixedTimestep=function(dn)local dp=1.0;local dq=1000.0;local dr=TimeSpan.FromSeconds(0.02)local ds=0xFFFF;local dt=a.CreateCircularQueue(64)local du=TimeSpan.FromSeconds(5)local dv=TimeSpan.FromSeconds(30)local dw=false;local dx=vci.me.Time;local dy=a.Random32()local dz=Vector3.__new(bit32.bor(0x400,bit32.band(dy,0x1FFF)),bit32.bor(0x400,bit32.band(bit32.rshift(dy,16),0x1FFF)),0.0)dn.SetPosition(dz)dn.SetRotation(Quaternion.identity)dn.SetVelocity(Vector3.zero)dn.SetAngularVelocity(Vector3.zero)dn.AddForce(Vector3.__new(0.0,0.0,dp*dq))local self={Timestep=function()return dr end,Precision=function()return ds end,IsFinished=function()return dw end,Update=function()if dw then return dr end;local dA=vci.me.Time-dx;local dB=dA.TotalSeconds;if dB<=Vector3.kEpsilon then return dr end;local dC=dn.GetPosition().z-dz.z;local dD=dC/dB;local dE=dD/dq;if dE<=Vector3.kEpsilon then return dr end;dt.Offer(dE)local dF=dt.Size()if dF>=2 and dA>=du then local dG=0.0;for T=1,dF do dG=dG+dt.Get(T)end;local dH=dG/dF;local dI=0.0;for T=1,dF do dI=dI+(dt.Get(T)-dH)^2 end;local dJ=dI/dF;if dJ<ds then ds=dJ;dr=TimeSpan.FromSeconds(dH)end;if dA>dv then dw=true;dn.SetPosition(dz)dn.SetRotation(Quaternion.identity)dn.SetVelocity(Vector3.zero)dn.SetAngularVelocity(Vector3.zero)end else dr=TimeSpan.FromSeconds(dE)end;return dr end}return self end,AlignSubItemOrigin=function(dK,dL,dM)local dN=dK.GetRotation()if not a.QuaternionApproximatelyEquals(dL.GetRotation(),dN)then dL.SetRotation(dN)end;local dO=dK.GetPosition()if not a.VectorApproximatelyEquals(dL.GetPosition(),dO)then dL.SetPosition(dO)end;if dM then dL.SetVelocity(Vector3.zero)dL.SetAngularVelocity(Vector3.zero)end end,CreateSubItemGlue=function()local dP={}local self;self={Contains=function(dQ,dR)return a.NillableIfHasValueOrElse(dP[dQ],function(bq)return a.NillableHasValue(bq[dR])end,function()return false end)end,Add=function(dQ,dS,dM)if not dQ or not dS then local dT=\'SubItemGlue.Add: Invalid arguments \'..(not dQ and\', parent = \'..tostring(dQ)or\'\')..(not dS and\', children = \'..tostring(dS)or\'\')error(dT,2)end;local bq=a.NillableIfHasValueOrElse(dP[dQ],function(dU)return dU end,function()local dU={}dP[dQ]=dU;return dU end)if type(dS)==\'table\'then for a4,aI in pairs(dS)do bq[aI]={velocityReset=not not dM}end else bq[dS]={velocityReset=not not dM}end end,Remove=function(dQ,dR)return a.NillableIfHasValueOrElse(dP[dQ],function(bq)if a.NillableHasValue(bq[dR])then bq[dR]=nil;return true else return false end end,function()return false end)end,RemoveParent=function(dQ)if a.NillableHasValue(dP[dQ])then dP[dQ]=nil;return true else return false end end,RemoveAll=function()dP={}return true end,Each=function(b1,dV)return a.NillableIfHasValueOrElse(dV,function(dQ)return a.NillableIfHasValue(dP[dQ],function(bq)for dR,dW in pairs(bq)do if b1(dR,dQ,self)==false then return false end end end)end,function()for dQ,bq in pairs(dP)do if self.Each(b1,dQ)==false then return false end end end)end,Update=function(dX)for dQ,bq in pairs(dP)do local dY=dQ.GetPosition()local dZ=dQ.GetRotation()for dR,dW in pairs(bq)do if dX or dR.IsMine then if not a.QuaternionApproximatelyEquals(dR.GetRotation(),dZ)then dR.SetRotation(dZ)end;if not a.VectorApproximatelyEquals(dR.GetPosition(),dY)then dR.SetPosition(dY)end;if dW.velocityReset then dR.SetVelocity(Vector3.zero)dR.SetAngularVelocity(Vector3.zero)end end end end end}return self end,\nCreateUpdateRoutine=function(d_,e0)return coroutine.wrap(function()local e1=TimeSpan.FromSeconds(30)local e2=vci.me.UnscaledTime;local e3=e2;local ct=vci.me.Time;local e4=true;while true do local cW=a.InstanceID()if cW~=\'\'then break end;local e5=vci.me.UnscaledTime;if e5-e1>e2 then a.LogError(\'TIMEOUT: Could not receive Instance ID.\')return-1 end;e3=e5;ct=vci.me.Time;e4=false;coroutine.yield(100)end;if e4 then e3=vci.me.UnscaledTime;ct=vci.me.Time;coroutine.yield(100)end;a.NillableIfHasValue(e0,function(e6)e6()end)while true do local cx=vci.me.Time;local e7=cx-ct;local e5=vci.me.UnscaledTime;local e8=e5-e3;d_(e7,e8)ct=cx;e3=e5;coroutine.yield(100)end end)end,CreateSlideSwitch=function(e9)local ea=a.NillableValue(e9.colliderItem)local eb=a.NillableValue(e9.baseItem)local ec=a.NillableValue(e9.knobItem)local ed=a.NillableValueOrDefault(e9.minValue,0)local ee=a.NillableValueOrDefault(e9.maxValue,10)if ed>=ee then error(\'SlideSwitch: Invalid argument: minValue >= maxValue\',2)end;local ef=(ed+ee)*0.5;local eg=function(aI)local eh,ei=a.PingPong(aI-ed,ee-ed)return eh+ed,ei end;local W=eg(a.NillableValueOrDefault(e9.value,0))local ej=a.NillableIfHasValueOrElse(e9.tickFrequency,function(ek)if ek<=0 then error(\'SlideSwitch: Invalid argument: tickFrequency <= 0\',3)end;return math.min(ek,ee-ed)end,function()return(ee-ed)/10.0 end)local el=a.NillableIfHasValueOrElse(e9.tickVector,function(c0)return Vector3.__new(c0.x,c0.y,c0.z)end,function()return Vector3.__new(0.01,0.0,0.0)end)local em=el.magnitude;if em<Vector3.kEpsilon then error(\'SlideSwitch: Invalid argument: tickVector is too small\',2)end;local en=a.NillableValueOrDefault(e9.snapToTick,true)local eo=e9.valueTextName;local ep=a.NillableValueOrDefault(e9.valueToText,tostring)local eq=TimeSpan.FromMilliseconds(1000)local er=TimeSpan.FromMilliseconds(50)local es,et;local dk={}local self;local eu=false;local ev=0;local ew=false;local ex=TimeSpan.Zero;local ey=TimeSpan.Zero;local ez=function(eA,eB)if eB or eA~=W then local dl=W;W=eA;for dm,a9 in pairs(dk)do dm(self,W,dl)end end;ec.SetLocalPosition((eA-ef)/ej*el)if eo then vci.assets.SetText(eo,ep(eA,self))end end;local eC=function()local eD=es()local eE,eF=eg(eD)local eG=eD+ej;local eH,eI=eg(eG)assert(eH)local eA;if eF==eI or eE==ee or eE==ed then eA=eG else eA=eF>=0 and ee or ed end;ey=vci.me.UnscaledTime;if eA==ee or eA==ed then ex=ey end;et(eA)end;a.NillableIfHasValueOrElse(e9.lsp,function(eJ)if not a.NillableHasValue(e9.propertyName)then error(\'SlideSwitch: Invalid argument: propertyName is nil\',3)end;local eK=a.NillableValue(e9.propertyName)es=function()return eJ.GetProperty(eK,W)end;et=function(aI)eJ.SetProperty(eK,aI)end;eJ.AddListener(function(br,a4,eL,eM)if a4==eK then ez(eg(eL),true)end end)end,function()local eL=W;es=function()return eL end;et=function(aI)eL=aI;ez(eg(aI),true)end end)self={GetColliderItem=function()return ea end,GetBaseItem=function()return eb end,GetKnobItem=function()return ec end,GetMinValue=function()return ed end,GetMaxValue=function()return ee end,GetValue=function()return W end,GetScaleValue=function(eN,eO)assert(eN<=eO)return eN+(eO-eN)*(W-ed)/(ee-ed)end,SetValue=function(aI)et(eg(aI))end,GetTickFrequency=function()return ej end,IsSnapToTick=function()return en end,AddListener=function(dm)dk[dm]=dm end,RemoveListener=function(dm)dk[dm]=nil end,DoUse=function()if not eu then ew=true;ex=vci.me.UnscaledTime;eC()end end,DoUnuse=function()ew=false end,DoGrab=function()if not ew then eu=true;ev=(W-ef)/ej end end,DoUngrab=function()eu=false end,Update=function()if eu then local eP=ea.GetPosition()-eb.GetPosition()local eQ=ec.GetRotation()*el;local eR=Vector3.Project(eP,eQ)local eS=(Vector3.Dot(eQ,eR)>=0 and 1 or-1)*eR.magnitude/em+ev;local eT=(en and a.Round(eS)or eS)*ej+ef;local eA=a.Clamp(eT,ed,ee)if eA~=W then et(eA)end elseif ew then local eU=vci.me.UnscaledTime;if eU>=ex+eq and eU>=ey+er then eC()end elseif ea.IsMine then a.AlignSubItemOrigin(eb,ea)end end}ez(eg(es()),false)return self end,CreateSubItemConnector=function()local eV=function(eW,dL,eX)eW.item=dL;eW.position=dL.GetPosition()eW.rotation=dL.GetRotation()eW.initialPosition=eW.position;eW.initialRotation=eW.rotation;eW.propagation=not not eX;return eW end;local eY=function(eZ)for dL,eW in pairs(eZ)do eV(eW,dL,eW.propagation)end end;local e_=function(p,ce,eW,f0,f1)local eP=p-eW.initialPosition;local f2=ce*Quaternion.Inverse(eW.initialRotation)eW.position=p;eW.rotation=ce;for dL,f3 in pairs(f0)do if dL~=eW.item and(not f1 or f1(f3))then f3.position,f3.rotation=a.RotateAround(f3.initialPosition+eP,f3.initialRotation,p,f2)dL.SetPosition(f3.position)dL.SetRotation(f3.rotation)end end end;local f4={}local f5=true;local f6=false;local self;self={IsEnabled=function()return f5 end,SetEnabled=function(bE)f5=bE;if bE then eY(f4)f6=false end end,Contains=function(f7)return a.NillableHasValue(f4[f7])end,Add=function(f8,f9)if not f8 then error(\'SubItemConnector.Add: Invalid argument: subItems = \'..tostring(f8),2)end;local fa=type(f8)==\'table\'and f8 or{f8}eY(f4)f6=false;for ai,dL in pairs(fa)do f4[dL]=eV({},dL,not f9)end end,Remove=function(f7)local K=a.NillableHasValue(f4[f7])f4[f7]=nil;return K end,RemoveAll=function()f4={}return true end,Each=function(b1)for dL,eW in pairs(f4)do if b1(dL,self)==false then return false end end end,GetItems=function()local fa={}for dL,eW in pairs(f4)do table.insert(fa,dL)end;return fa end,Update=function()if not f5 then return end;local fb=false;for dL,eW in pairs(f4)do local cZ=dL.GetPosition()local fc=dL.GetRotation()if not a.VectorApproximatelyEquals(cZ,eW.position)or not a.QuaternionApproximatelyEquals(fc,eW.rotation)then if eW.propagation then if dL.IsMine then e_(cZ,fc,f4[dL],f4,function(f3)if f3.item.IsMine then return true else f6=true;return false end end)fb=true;break else f6=true end else f6=true end end end;if not fb and f6 then eY(f4)f6=false end end}return self end,GetSubItemTransform=function(f7)local p=f7.GetPosition()local ce=f7.GetRotation()local fd=f7.GetLocalScale()return{positionX=p.x,positionY=p.y,positionZ=p.z,rotationX=ce.x,rotationY=ce.y,rotationZ=ce.z,rotationW=ce.w,scaleX=fd.x,scaleY=fd.y,scaleZ=fd.z}end}a.SetConstEach(a,{LogLevelOff=0,LogLevelFatal=100,LogLevelError=200,LogLevelWarn=300,LogLevelInfo=400,LogLevelDebug=500,LogLevelTrace=600,LogLevelAll=0x7FFFFFFF,ColorHueSamples=10,ColorSaturationSamples=4,ColorBrightnessSamples=5,EscapeSequenceTag=\'#__CYTANB\',SolidusTag=\'#__CYTANB_SOLIDUS\',NegativeNumberTag=\'#__CYTANB_NEGATIVE_NUMBER\',ArrayNumberTag=\'#__CYTANB_ARRAY_NUMBER\',InstanceIDParameterName=\'__CYTANB_INSTANCE_ID\',MessageValueParameterName=\'__CYTANB_MESSAGE_VALUE\',MessageSenderOverride=\'__CYTANB_MESSAGE_SENDER_OVERRIDE\',MessageOriginalSender=\'__CYTANB_MESSAGE_ORIGINAL_SENDER\',TypeParameterName=\'__CYTANB_TYPE\',ColorTypeName=\'Color\',Vector2TypeName=\'Vector2\',Vector3TypeName=\'Vector3\',Vector4TypeName=\'Vector4\',QuaternionTypeName=\'Quaternion\',DedicatedCommentMessageName=\'cytanb.comment.a2a6a035-6b8d-4e06-b4f9-07e6209b0639\',DedicatedNotificationMessageName=\'cytanb.notification.698ba55f-2b69-47f2-a68d-bc303994cff3\',LOCAL_SHARED_PROPERTY_EXPIRED_KEY=\'__CYTANB_LOCAL_SHARED_PROPERTY_EXPIRED\'})a.SetConstEach(a,{ColorMapSize=a.ColorHueSamples*a.ColorSaturationSamples*a.ColorBrightnessSamples,FatalLogLevel=a.LogLevelFatal,ErrorLogLevel=a.LogLevelError,WarnLogLevel=a.LogLevelWarn,InfoLogLevel=a.LogLevelInfo,DebugLogLevel=a.LogLevelDebug,TraceLogLevel=a.LogLevelTrace})c=a.MakeSearchPattern({\'\\t\',\'\\n\',\'\\v\',\'\\f\',\'\\r\',\' \'},1,-1)d,e=(function()local fe={\'A\',\'B\',\'C\',\'D\',\'E\',\'F\',\'a\',\'b\',\'c\',\'d\',\'e\',\'f\',\'0\',\'1\',\'2\',\'3\',\'4\',\'5\',\'6\',\'7\',\'8\',\'9\'}return a.MakeSearchPattern(fe,4,4),a.MakeSearchPattern(fe,8,8)end)()f=a.MakeSearchPattern({\'A\',\'B\',\'C\',\'D\',\'E\',\'F\',\'G\',\'H\',\'I\',\'J\',\'K\',\'L\',\'M\',\'N\',\'O\',\'P\',\'Q\',\'R\',\'S\',\'T\',\'U\',\'V\',\'W\',\'X\',\'Y\',\'Z\',\'a\',\'b\',\'c\',\'d\',\'e\',\'f\',\'g\',\'h\',\'i\',\'j\',\'k\',\'l\',\'m\',\'n\',\'o\',\'p\',\'q\',\'r\',\'s\',\'t\',\'u\',\'v\',\'w\',\'x\',\'y\',\'z\',\'0\',\'1\',\'2\',\'3\',\'4\',\'5\',\'6\',\'7\',\'8\',\'9\',\'_\',\'-\',\'.\',\'(\',\')\',\'!\',\'~\',\'*\',\'\\\'\',\'%\'},1,-1)g={{tag=a.NegativeNumberTag,search=a.NegativeNumberTag,replacement=\'\'},{tag=a.ArrayNumberTag,search=a.ArrayNumberTag,replacement=\'\'},{tag=a.SolidusTag,search=a.SolidusTag,replacement=\'/\'},{tag=a.EscapeSequenceTag,search=a.EscapeSequenceTag..a.EscapeSequenceTag,replacement=a.EscapeSequenceTag}}h={[a.ColorTypeName]={compositionFieldNames=a.ListToMap({\'r\',\'g\',\'b\',\'a\'}),compositionFieldLength=4,toTableFunc=a.ColorToTable,fromTableFunc=a.ColorFromTable},[a.Vector2TypeName]={compositionFieldNames=a.ListToMap({\'x\',\'y\'}),compositionFieldLength=2,toTableFunc=a.Vector2ToTable,fromTableFunc=a.Vector2FromTable},[a.Vector3TypeName]={compositionFieldNames=a.ListToMap({\'x\',\'y\',\'z\'}),compositionFieldLength=3,toTableFunc=a.Vector3ToTable,fromTableFunc=a.Vector3FromTable},[a.Vector4TypeName]={compositionFieldNames=a.ListToMap({\'x\',\'y\',\'z\',\'w\'}),compositionFieldLength=4,toTableFunc=a.Vector4ToTable,fromTableFunc=a.Vector4FromTable},[a.QuaternionTypeName]={compositionFieldNames=a.ListToMap({\'x\',\'y\',\'z\',\'w\'}),compositionFieldLength=4,toTableFunc=a.QuaternionToTable,fromTableFunc=a.QuaternionFromTable}}i=a.ListToMap({a.NegativeNumberTag,a.ArrayNumberTag})j=a.LogLevelInfo;l={[a.LogLevelFatal]=\'FATAL\',[a.LogLevelError]=\'ERROR\',[a.LogLevelWarn]=\'WARN\',[a.LogLevelInfo]=\'INFO\',[a.LogLevelDebug]=\'DEBUG\',[a.LogLevelTrace]=\'TRACE\'}package.loaded[\'cytanb\']=a;m,n=(function()local dd=\'eff3a188-bfc7-4b0e-93cb-90fd1adc508c\'local dj=_G[dd]if not dj then dj={}_G[dd]=dj end;local ff=dj.randomSeedValue;if not ff then ff=os.time()-os.clock()*10000;dj.randomSeedValue=ff;math.randomseed(ff)end;local fg=dj.clientID;if type(fg)~=\'string\'then fg=tostring(a.RandomUUID())dj.clientID=fg end;local fh=vci.state.Get(b)or\'\'if fh==\'\'and vci.assets.IsMine then fh=tostring(a.RandomUUID())vci.state.Set(b,fh)end;return fh,fg end)()return a end)()\n\nif vci.assets.IsMine then\n";
            
                // 接続時は最新データーを１件
                // それ以外はたまっていたもの全部
                if(connected == 0)
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
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n設定ファイルがありません。\nThere is no setting file.\n\n1. …\\MultiCommentViewer\\plugins\\MtoV\\MtoV.txt を作成してください。\n   Please create the text file.\n\n2. MtoV.txt に CommentBaton VCI の場所 C:\\Users\\%ユーザー名%\\AppData\\LocalLow\\infiniteloopCo,Ltd\\VirtualCast\\EmbeddedScriptWorkspace\\CommentBaton を書いてください。\n   Please write the CommentBaton VCI directory in the text file.\n\n3. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
            else if (errorNumber == 2)
            {
                // プラグイン停止
                ONOFF = false;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n指定ディレクトリが CommentBaton ではありません。\nThe directory is not CommentBaton.\n\n1. MtoV.txt の内容（ CommentBaton VCI の場所 ）を確認してください。\n   Please check the CommentBaton directory in the MtoV.txt.\n\n2. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
            else if (errorNumber == 3)
            {
                // プラグイン停止
                ONOFF = false;
                // タイマー停止
                timer.Stop();
                MessageBox.Show("プラグインを停止しました。\nThis plugin was stopped\n\n指定ディレクトリがありません。\nThe directory does not Exist.\n\n1. MtoV.txt の内容（ CommentBaton VCI の場所 ）と実在を確認してください。\n   Please check the CommentBaton directory in the MtoV.txt and existence.\n\n2. MCVを立ち上げなおしてください。\n   Please reboot MCV.", "MtoV エラー error");
            }
        }
    }
}