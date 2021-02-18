using System;
using System.Windows.Forms;
using System.Drawing;

namespace MtoVPlugin
{
    public partial class Form1 : Form
    {
        private Class1 _class1;
        public int tMode;

        /// <summary>
        /// 設定ウィンドウを開く
        /// </summary>
        public Form1(Class1 c)
        {
            _class1 = c; // メインへの参照を保存
            tMode = _class1.transferMode;
            this.Width = 612;
            this.Height = 244;

            setupControls();
        }

        /// <summary>
        /// 設定ウィンドウの中身
        /// </summary>
        public void setupControls()
        {
            GroupBox group;
            RadioButton radio0, radio1, radio2;
            Button bt0;

            group = new GroupBox();
            group.Width = 580;
            group.Height = 136;
            group.Top = 8;
            group.Left = 8;
            group.Text = "転送モード Transfer mode";
            this.Controls.Add(group);

            radio0 = new RadioButton();
            radio0.Name = "0";
            radio0.Width = 520;
            radio0.Height = 35;
            radio0.Text = "0:転送しない OFF";
            radio0.Top = 20;
            radio0.Left = 25;
            if (tMode == 0)
            {
                radio0.Checked = true;
            }
            else
            {
                radio0.Checked = false;
            }
            radio0.CheckedChanged += check_changed;
            group.Controls.Add(radio0);
            radio1 = new RadioButton();
            radio1.Name = "1";
            radio1.Width = 520;
            radio1.Height = 35;
            radio1.Text = "1:部分転送 Part   [Ni:運営 Special] [SH:無 N/A] [Yt,Tc,Tw,Wh,OR:全 All]";
            radio1.Top = 50;
            radio1.Left = 25;
            if (tMode == 1)
            {
                radio1.Checked = true;
            }
            else
            {
                radio1.Checked = false;
            }
            radio1.CheckedChanged += check_changed;
            group.Controls.Add(radio1);
            radio2 = new RadioButton();
            radio2.Name = "2";
            radio2.Width = 520;
            radio2.Height = 35;
            radio2.Text = "2:全転送 ALL";
            radio2.Top = 85;
            radio2.Left = 25;
            if (tMode == 2)
            {
                radio2.Checked = true;
            }
            else
            {
                radio2.Checked = false;
            }
            radio2.CheckedChanged += check_changed;
            group.Controls.Add(radio2);

            // 閉じる(適用)ボタン
            bt0 = new Button();
            bt0.Location = new Point(200, 146);
            bt0.Size = new Size(144, 46);
            bt0.Text = "閉じる(適用)\nClose(Apply)";
            bt0.Click += new EventHandler(bt0_Click);
            this.Controls.Add(bt0);
        }

        /// <summary>
        /// ラジオボタンが押された
        /// </summary>
        /// 
        private void check_changed(object sender, EventArgs e)
        {
            RadioButton btn = (RadioButton)sender;
            tMode = int.Parse(btn.Name);
        }

        /// <summary>
        /// 閉じる(適用)ボタンが押された
        /// </summary>
        private void bt0_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}