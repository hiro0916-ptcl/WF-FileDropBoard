﻿//
//  FileDropBoard - v 1.1.1 @ Stable
//    by 2012 - 2017 Hiro-Project
//  GitHub : https://github.com/hiro0916-ptcl/WF-FileDropBoard 
//
//  LICENSED under the GPL-3.0
//   - See : https://www.gnu.org/licenses/gpl-3.0.en.html
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using FData;
using System.Threading.Tasks;

namespace WF_FileDropBoard
{
    public partial class Main : Form {

        FileMenu FM;

        //バージョンと割り当て番号
        public const string VersionS = "1.1.1";
        public const int VerNum = 3;

        public string FilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\";
        public string FileName = "Setting.json";

        private int MenuColor_R = 0;
        private int MenuColor_G = 150;
        private int MenuColor_B = 250;

        // 演出用
        public enum ProductionModeE {
            None,  //してないよ
            Enter, //入ってくるメニュー
            Wait,  //待機中
            Leave  //退場するメニュー
        };

        private int MenuButtonProductionTime = 0;
        private ProductionModeE MenuButtonProductionMode = ProductionModeE.None;

        private int MenuProductionTime = 0;
        public ProductionModeE MenuProductionMode = ProductionModeE.None;

        private int DragProductionTime = 0;
        private int DragProductionMaxTime = 20;
        private ProductionModeE DragProductionMode = ProductionModeE.None;

        private bool IsMenuOpenB = false;
        public bool IsSettingsOpenB = false;
        //FileData FileListS = new FileData();

        public List<FileData> FileListS = new List<FileData>();


        //デフォルトの拡張子別タイル色の設定
        public Dictionary<String, Color> ExtCol = new Dictionary<String, Color>() {
            {".txt", Color.FromArgb(50,150,50)  },
            {".lnk", Color.FromArgb(150,100,100)},
            {".docx",Color.FromArgb(52,96,163)  },
            {".xlsx",Color.FromArgb(41,124,80)  },
            {".pptx",Color.FromArgb(218,90,48)  },
            {".exe", Color.FromArgb(127,35,198) }
        };

        //通知の色設定
        public Color Noti_InfoColor = Color.FromArgb( 50, 150, 200);
        public Color Noti_SuccColor = Color.FromArgb( 50, 150,  50);
        public Color Noti_WarnColor = Color.FromArgb(180, 70,   0);

        //タイルの大きさの設定
        //変更できるようにするよ
        public int TileWidth = 110;
        public int TileHeight = 110;

        private int TileDiffX;
        private int TileDiffY;
        HashSet<int> UsedFileNumS = new HashSet<int>();
        private int CurrentFileNum = 0;
        private int SelectedFileNum = -1;
        private bool MouseDragging = false;
        private bool AlreadyTimed = false;
        private int InfoLoopCount = 0;
        private int InfoMaxLoopCount = 10;
        public List<string> InfoUsingControls = new List<string>();
        public ProductionModeE NotiProductionMode = ProductionModeE.None;

        private System.Windows.Forms.TableLayoutPanel SaveNotiTLP;
        public List<string> Logs = new List<string>();

        // 表示系設定
        int DragUpdateTick = 15;

        public string File_DateFormatS = "MM/dd";
        public string File_TimeFormatS = "HH:mm";

        public bool File_AlwaysShowDate = true;
        public bool File_ShowPreview = true;
        public enum File_ShowDateModeE { //タイルに表示するもの
            None,         //何もなし
            DateOnly,     //日付のみ
            TimeOnly,     //時刻のみ
            DateAndTime   //日付と時刻
        }
        public File_ShowDateModeE File_ShowDateMode = File_ShowDateModeE.DateAndTime;
        //ウィンドウ系設定
        public bool TopWindow = false;
        public bool AutoHide = false;
        public int AutoHideTime = 5;
        public enum AutoHidePositionE {
            UpperLeft,
            UnderLeft,
            UpperRight,
            UnderRight
        }
        public AutoHidePositionE AutoHidePosition = AutoHidePositionE.UnderRight;

        public Main() {
            InitializeComponent();
        }

        //ドラッグされた....これって入る？
        private void MainBox_DragEnter(object sender, DragEventArgs e) {
            //Debug.Print("{0},{1}", MouseDragging, DenyDragging);
            // if ((MouseDragging == false) && (DenyDragging == false)) { // 複製されちゃ困るので
            if (MouseDragging == false) { // 複製されちゃ困るので
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) {　//これは....ファイル？
                    DragProductionMode = ProductionModeE.Enter; //一応演出をかける
                    DragProductionTimer.Start();
                    DragProductionTime = 0;
                    e.Effect = DragDropEffects.Copy; //コピーっぽい
                    //Debug.Print("Drag:Allow");
                } else { //ファイルじゃない
                    e.Effect = DragDropEffects.None;
                }
            } else { //そもそも複製しようとしてた
                e.Effect = DragDropEffects.None;
            }
        }

        private async void MainBox_DragDrop(object sender, DragEventArgs e) {
            // フォーム上のどこにファイルを置くか？
            //if (DenyDragging == false) {
            Point CursorPosition = Cursor.Position;
            Point CursorinFormPosition = this.PointToClient(CursorPosition);

            int ToX = CursorinFormPosition.X - TileWidth / 2;
            int ToY = CursorinFormPosition.Y - TileHeight / 2;

            // ドラッグされたファイル名を取得
            string[] FileName = (string[])e.Data.GetData(DataFormats.FileDrop, false);
            foreach (string FileItemName in FileName) {
                FileData TempFileData = new FileData {
                    FilePath = FileItemName,
                    //FileName = System.IO.Path.GetFileName(FileItemName),
                    //FileExt = System.IO.Path.GetExtension(FileItemName),
                    PosX = ToX,
                    PosY = ToY
                };
                await AddFile(TempFileData);

            }

            MainGRPBox.Refresh();
            //} else {
            //    DenyDragging = false;
            //}
        }

        private void MainBox_DragLeave(object sender, EventArgs e) {
            DragProductionMode = ProductionModeE.Leave;
            DragProductionTimer.Start();
            DragProductionTime = DragProductionMaxTime;
        }

        /// <summary>
        /// ファイルをボードに追加します。
        /// </summary>
        /// <param name="AddFileData">追加したいファイルのデータ。</param>
        public async Task AddFile(FileData AddFileData) {
            try {
                AddFileData.FileCol = ExtCol[AddFileData.FileExt];
            } catch (KeyNotFoundException) {
                AddFileData.FileCol = Color.FromArgb(150, 150, 150);
            }
            //               AddFileData.FileNum = CurrentFileNum;
            for (var i = 0; i < 2000; i++) {
                if (UsedFileNumS.Add(CurrentFileNum)) {
                    AddFileData.FileNum = CurrentFileNum;
                    break;
                } else {
                    CurrentFileNum++;
                }
            }
            //AddFileData.FileLastUpdateTime = new System.IO.FileInfo(AddFileData.FilePath + AddFileData.FileName).LastWriteTime;
            //                CurrentFileNum++;
            FileListS.Add(AddFileData);
            FileData FD = FileListS[FileListS.Count - 1];
            FD.FilePreviewS = await GetPreview(FileListS.Count - 1);
        }


        private void MainBox_MouseLeave(object sender, EventArgs e) {
            DisposeBox.Visible = false;
            DragUpdateTimer.Stop();
        }

        private void Main_MouseEnter(object sender, EventArgs e) {
            AutoHideTimer.Stop();
        }

        private void MainBox_Deactivate(object sender, EventArgs e) {
            MouseDragging = false;
            DisposeBox.Visible = false;
            SelectedFileNum = -1;
            DragUpdateTimer.Stop();
            MainGRPBox.Invalidate();
            if (AutoHide) { //自動で隠す
                AutoHideTimer.Start();
            }
        }

        //
        // MainGRPBox : グラフィック画面
        //
        private void MainGRPBox_Paint(object sender, PaintEventArgs e) {
            
            Graphics GRP = e.Graphics;
            GRP.Clear(Color.FromArgb(255, 255, 255, 255));
            // GRP.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            FileData WriteFileData;
            for (var i = FileListS.Count - 1; i >= 0; i--) {
                WriteFileData = FileListS[i];
                SolidBrush FileBackBrush = new SolidBrush(WriteFileData.FileCol);
                if (SelectedFileNum == i) {
                    FileBackBrush = new SolidBrush(Color.FromArgb(WriteFileData.FileCol.R / 2, WriteFileData.FileCol.G / 2, WriteFileData.FileCol.B / 2));
                }// else {
                //    SolidBrush FileBackBrush = new SolidBrush(WriteFileData.FileCol);
                //}
                SolidBrush FileNameBrush = new SolidBrush(Color.FromArgb(255, 255, 255));
                int WritableY = TileHeight; //タイルの中で描画可能な領域
                if (File_AlwaysShowDate == true) { // 日付を表示するモードだったら
                    WritableY -= this.Font.Height; //領域確保
                }
                GRP.FillRectangle(FileBackBrush, WriteFileData.PosX, WriteFileData.PosY, TileWidth, TileHeight);
                //ファイル名描画
                SizeF NameSize = GRP.MeasureString(WriteFileData.FileName, this.Font, WritableY);
                RectangleF Namerect = new RectangleF(WriteFileData.PosX, WriteFileData.PosY, TileWidth, NameSize.Height);
                GRP.DrawString(WriteFileData.FileName, this.Font, FileNameBrush, Namerect);
                //プレビュー描画
                SolidBrush FilePreviewBrush = new SolidBrush(Color.FromArgb(192, 192, 192));
                if (File_ShowPreview) {
                    RectangleF PrevRect = new RectangleF(WriteFileData.PosX, WriteFileData.PosY + NameSize.Height, TileWidth, WritableY - NameSize.Height);
                    GRP.DrawString(WriteFileData.FilePreviewS, this.Font, FilePreviewBrush, PrevRect);
                }

                //日付...描画する？
                if (File_AlwaysShowDate == true) {
                    string WriteDateS = "";

                    switch (File_ShowDateMode) {

                        case File_ShowDateModeE.None:
                        //何も表示しない
                        break;

                        case File_ShowDateModeE.DateOnly:
                        //日付だけ表示する
                        WriteDateS = WriteFileData.FileLastUpdateTime.ToString(File_DateFormatS);
                        break;

                        case File_ShowDateModeE.TimeOnly:
                        //時刻だけ表示する
                        WriteDateS = WriteFileData.FileLastUpdateTime.ToString(File_TimeFormatS);
                        break;

                        case File_ShowDateModeE.DateAndTime:
                        //日付と時刻、両方を表示する
                        WriteDateS = WriteFileData.FileLastUpdateTime.ToString(File_DateFormatS) + " " + WriteFileData.FileLastUpdateTime.ToString(File_TimeFormatS);
                        break;

                        //たぶんここには来ない
                        default:
                        break;

                    }

                    //LastUpdate
                    WriteDateS = "LU:" + WriteDateS;

                    RectangleF DateRect = new RectangleF(WriteFileData.PosX, WriteFileData.PosY + TileHeight - this.Font.Height, TileWidth, this.Font.Height);
                    GRP.DrawString(WriteDateS, this.Font, FilePreviewBrush, DateRect);

                }
                FilePreviewBrush.Dispose();
                FileBackBrush.Dispose();
                FileNameBrush.Dispose();

            }
            if (FileListS.Count > 0) {
                DDLabel.Visible = false;
            } else {
                DDLabel.Visible = true;
            }
            //}
            //GRP.Dispose();
            
        }

        private void MainGRPBox_Click(object sender, EventArgs e) {
            //Debug.Print("Clicked");
            if (IsMenuOpenB == true) {
                IsMenuOpenB = false;
                MenuProductionMode = ProductionModeE.Leave;
                MenuProductionTimer.Start();
            }
            //無理やり通知を閉じる
            NotiCloseTimer.Stop();
            NotiCloseTimer.Interval = 1;
            NotiCloseTimer.Start();
        }

        private void MainGRPBox_MouseUp(object sender, MouseEventArgs e) {
            MouseDragging = false;
            DisposeBox.Visible = false;
            if (SelectedFileNum != -1) {
                FileData TempFD = FileListS[SelectedFileNum];
                Point CursorPosition = PointToClient(Cursor.Position);
                int CursorX = DisposeBox.Location.X;
                int CursorY = DisposeBox.Location.Y;
                int TileX = TempFD.PosX;
                int TileY = TempFD.PosY;

                if (( CursorX > TileX ) && ( CursorX < ( TileX + DisposeBox.Size.Width ) ) && ( CursorY > TileY ) && ( CursorY < ( TileY + DisposeBox.Size.Height ) )) {
                    FileListS.RemoveAt(SelectedFileNum);
                }
            }
            //DenyDragging = false;
            if (AlreadyTimed == false) {
                // クリックされたときの処理として
            } else {
                // ドラッグおしまい

            }

            SelectedFileNum = -1;
            DragUpdateTimer.Stop();
            MainGRPBox.Invalidate();
            //Debug.Print("Dragging Stopped");
        }

        private void MainGRPBox_MouseDown(object sender, MouseEventArgs e) {
            int Len = FileListS.Count;
            FileData CurrentFD;
            Point CursorPosition = PointToClient(Cursor.Position);
            int CursorX = CursorPosition.X;
            int CursorY = CursorPosition.Y;
            int TileX;
            int TileY;
            int TileWidth = this.TileWidth;
            int TileHeight = this.TileHeight;

            //何がドラッグされたか調べる
            for (var i = 0; i < Len; i++) {
                CurrentFD = FileListS[i];
                TileX = CurrentFD.PosX;
                TileY = CurrentFD.PosY;
                if (( CursorX > TileX ) && ( CursorX < ( TileX + TileWidth ) ) && ( CursorY > TileY ) && ( CursorY < ( TileY + TileHeight ) )) {
                    SelectedFileNum = 0;
                    FileListS.Remove(CurrentFD);
                    FileListS.Insert(0, CurrentFD);
                    TileDragTimer.Start();
                    DragUpdateTimer.Start();
                    AlreadyTimed = false;
                    this.TileDiffX = CursorX - TileX;
                    this.TileDiffY = CursorY - TileY;
                    MouseDragging = true;
                    DisposeBox.Visible = true;

                    //Debug.Print("Num {0} was selected", i);
                    MainGRPBox.Invalidate();

                    break;
                }
            }
        }

        private void MenuPic_MouseEnter(object sender, EventArgs e) {
            MenuButtonProductionMode = ProductionModeE.Enter;
            MenuButtonProductionTimer.Start();

            //Debug.Print(MenuButtonProductionTime.ToString());
        }

        private void MenuPic_MouseLeave(object sender, EventArgs e) {
            MenuButtonProductionMode = ProductionModeE.Leave;
            MenuButtonProductionTimer.Start();

            //Debug.Print(MenuButtonProductionTime.ToString());
        }


        private void MenuButtonProductionTimer_Tick(object sender, EventArgs e) {
            //Debug.Print(MenuButtonProductionTime.ToString());
            switch (MenuButtonProductionMode) {


                case ProductionModeE.None:
                break;

                case ProductionModeE.Enter:
                MenuButtonProductionTime += 20;
                MenuButtonProductionTimer.Start();
                if (MenuButtonProductionTime >= 20) {
                    // 例外(続行できる) : MenuButtonProductionTime が範囲外の値(21以上)を示した
                    if (MenuButtonProductionTime > 20) {
                        Debug.Print("[!]例外が発生しました - MenuButtonProductionTime の値が {0} を示しました", MenuButtonProductionTime);
                        Debug.Print("   これにより MenuButtonProductionTimer は強制停止されます。");
                        MenuButtonProductionTime = 20; // 例外を回避したい
                        MenuButtonProductionTimer.Stop(); // 強制停止
                    }
                    MenuButtonProductionTimer.Stop();
                    MenuButtonProductionMode = ProductionModeE.None;
                    MenuPic.Refresh();
                }
                break;

                case ProductionModeE.Leave:
                MenuButtonProductionTime -= 20;
                MenuButtonProductionTimer.Start();
                if (MenuButtonProductionTime <= 0) {
                    // 例外(続行できる) : MenuButtonProductionTime が範囲外の値(0未満)を示した
                    if (MenuButtonProductionTime < 0) {
                        Debug.Print("[!]例外が発生しました - MenuButtonProductionTime の値が {0} を示しました", MenuButtonProductionTime);
                        Debug.Print("   これにより MenuButtonProductionTimer は強制停止されます。");
                        MenuButtonProductionTime = 0; // 例外を回避したい
                        MenuButtonProductionTimer.Stop(); // Timer の暴走を強制停止させる
                    }

                    MenuButtonProductionTimer.Stop();
                    MenuButtonProductionMode = ProductionModeE.None;
                    MenuPic.Refresh();
                }
                break;
            }
            //Debug.Print("C");

        }

        private void MenuPic_Paint(object sender, PaintEventArgs e) {
            Graphics MenuGRP = e.Graphics;
            MenuGRP.Clear(Color.FromArgb(255, 255, 255));
            MenuGRP.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            //SolidBrush MenuBrush = new SolidBrush(Color.FromArgb(255, MenuColor_R, MenuColor_G, MenuColor_B));

            double ColT = 1.0 + MenuButtonProductionTime / 20;
            SolidBrush MenuButtonBrush = new SolidBrush(Color.FromArgb(255, (int)( ( MenuColor_R ) / ColT ), (int)( ( MenuColor_G ) / ColT ), (int)( ( MenuColor_B ) / ColT )));
            //Pen MenuPen = new Pen(MenuButtonBrush, 1);

            int ColRGB = 255; //(int)(255 - (MenuButtonProductionTime * 11));
            SolidBrush MenuButtonBarBrush = new SolidBrush(Color.FromArgb(255, ColRGB, ColRGB, ColRGB));
            //Pen MenuButtonBarPen = new Pen(MenuButtonBarBrush, 1);

            //int a = 130;
            SolidBrush MenuButtonShadowBrush = new SolidBrush(Color.FromArgb(30 + MenuButtonProductionTime, 0, 0, 0));
            for (int a = 130; a > 0; a -= 30) {
                //Pen MenuButtonShadowPen = new Pen(MenuButtonShadowBrush, 1);
                MenuGRP.FillPie(MenuButtonShadowBrush, 0, 0 + ( a / 20 ), 60, 60, 0, 180);
            }
            /*
            SolidBrush MenuButtonShadowBrush = new SolidBrush(Color.FromArgb(200, MenuColor_R / 2, MenuColor_G / 2, MenuColor_B / 2));
            Pen MenuButtonShadowPen = new Pen(MenuButtonShadowBrush, 1);
            MenuGRP.FillEllipse(MenuButtonShadowBrush, Width - 85 + 1, Height - 110 + 1, 60, 60);
            */

            MenuGRP.FillEllipse(MenuButtonBrush, 0, 0, 60, 60);

            for (var i = 0; i < 3; i++) {
                MenuGRP.FillRectangle(MenuButtonBarBrush, 20, 20 + i * 8, 22, 4);
            }

            MenuButtonBrush.Dispose();
            MenuButtonBarBrush.Dispose();
            MenuButtonShadowBrush.Dispose();
            //Debug.Print("Painted");
        }

        private void MenuPic_Click(object sender, EventArgs e) {
            //Debug.Print("Clicked");
            FM = new FileMenu(this) {
                TopLevel = false,
                Visible = false
            };
            this.Controls.Add(FM);
            FM.Font = this.Font;
            FM.Show();
            FM.BringToFront();
            FM.Location = new Point(Width, 0);
            FM.Size = new Size(150, Height - 36);
            FM.Visible = true;
            MenuProductionMode = ProductionModeE.Enter;
            MenuProductionTimer.Start();
            IsMenuOpenB = true;
        }

        private void MenuProductionTimer_Tick(object sender, EventArgs e) {
            FM.Location = new Point((int)( Width - ( MenuProductionTime * 18.75 ) ), 0);
            //Debug.Print(MenuProductionTime.ToString());

            switch (MenuProductionMode) {
                case ProductionModeE.None:
                break;

                case ProductionModeE.Enter:
                MenuProductionTime++;
                if (MenuProductionTime > 8) {
                    MenuProductionMode = ProductionModeE.None;
                    MenuProductionTimer.Stop();
                    MenuProductionTime = 8;
                }
                break;

                case ProductionModeE.Leave:
                MenuProductionTime--;
                if (MenuProductionTime <= 0) {
                    MenuProductionMode = ProductionModeE.None;
                    MenuProductionTimer.Stop();
                    FM.Dispose();
                }
                break;


            }
        }

        private void DragProductionTimer_Tick(object sender, EventArgs e) {
            //Graphics GRP = CreateGraphics();
            //GRP.Clear(Color.FromArgb(255, 255, 255, 255));
            /*
            int BackR = 150;
            int BackG = 200;
            int BackB = 250;
            */
            double TimePer = DragProductionTime / DragProductionMaxTime;

            //SolidBrush BackBrush = new SolidBrush(Color.FromArgb((int)(255 * TimePer), BackR, BackG, BackB));

            //BackBrush.Dispose();
            //GRP.Dispose();

            //Debug.Print(DragProductionTime.ToString());

            switch (DragProductionMode) {
                case ProductionModeE.None:
                break;

                case ProductionModeE.Enter:
                DragProductionTime++;
                if (DragProductionTime > DragProductionMaxTime) {
                    DragProductionMode = ProductionModeE.None;
                    DragProductionTimer.Stop();
                    DragProductionTime = DragProductionMaxTime;
                }
                break;

                case ProductionModeE.Leave:
                DragProductionTime--;
                if (DragProductionTime <= 0) {
                    DragProductionMode = ProductionModeE.None;
                    DragProductionTimer.Stop();
                }
                break;

            }
        }

        private void TileDragTimer_Tick(object sender, EventArgs e) {
            AlreadyTimed = true;
            TileDragTimer.Stop();
            //Debug.Print("Dragging Started");
            MouseDragging = true;
            DisposeBox.Visible = true;
        }

        private void DragUpdateTimer_Tick(object sender, EventArgs e) {

            FileData FD = FileListS[SelectedFileNum];
            FileData ToFD = FD;
            Point ToPosition = PointToClient(Cursor.Position);
            int toX = ToPosition.X - TileDiffX;
            int toY = ToPosition.Y - TileDiffY;
            //Debug.Print("{0}/{1},{2}/{3}", ToPosition.X, Width - TileWidth, ToPosition.Y, Height - TileHeight);
            //Debug.Print("{0},{1}", toX, toY);
            if (( ( ToPosition.X >= ( this.Width ) ) || ( ToPosition.Y >= ( this.Height - SystemInformation.CaptionHeight ) ) || ( ToPosition.X <= 0 ) || ( ToPosition.Y <= -SystemInformation.CaptionHeight ) )) {
                //          if ((toX > (Width - TileWidth)) || (toY > (Height - TileHeight)) || (toX < 0) || (toY < 0)) {
                // Debug.Print("Drag&Drop Leaving Started");
                String[] FileNames = { (string)FileListS[SelectedFileNum].FilePath };
                DataObject DraggingDataobject = new DataObject(DataFormats.FileDrop, FileNames);
                DragDropEffects dde = this.DoDragDrop(DraggingDataobject, DragDropEffects.Copy);
                //DenyDragging = true;
                DragUpdateTimer.Stop();
            }
            //ドラッグしてる - ゴミ箱表示
            if (MouseDragging == true) {
                toX = Math.Min(Math.Max(toX, 0), Width - TileWidth);
                toY = Math.Min(Math.Max(toY, 0), Height - TileHeight);
                ToFD.PosX = toX;
                ToFD.PosY = toY;
                if (( ToPosition.X > DisposeBox.Location.X ) && ( ToPosition.X < DisposeBox.Location.X + DisposeBox.Size.Width ) && ( ToPosition.Y > DisposeBox.Location.Y ) && ( ToPosition.Y < DisposeBox.Location.Y + DisposeBox.Size.Height )) {
                    DisposeBox.BackColor = Color.Red;
                } else {
                    //しない
                    DisposeBox.BackColor = Color.White;
                }
                //Debug.Print("{0}/{1},{2}/{3}", toX,Width - TileWidth, toY,Height - TileHeight);
            }
            if (SelectedFileNum != -1) {
                FileListS[SelectedFileNum] = ToFD;
                DragUpdateTimer.Start();
            }
            //Debug.Print("{0},{1}", toX, toY);

            MainGRPBox.Invalidate();
        }

        //プレビューを返すだけ
        //非同期なのかな....

        ///<summary>
        ///プレビューとして表示する文字列を取得します。
        ///</summary>
        ///<param name="Index"> - プレビューを取得したいファイルが FileListS 内のどこにあるかを指定するインデックス。</param>
        ///<returns>プレビューとして表示すべき文字列。</returns>
        public async Task<string> GetPreview(int Index) {
            //必要なデータ
            FileData TempFD = FileListS[Index];
            string FilePath = TempFD.FilePath;
            string PreviewedStringS = "プレビューできません";
            //最終更新時刻を個別で取得するようになったのでCO
            //string FileLastWriteDateS = "";

            /* 最終更新時刻を個別で取得するようになったのでCO
            if (File_AlwaysShowDate == true) {
                System.IO.FileInfo fi;
                try {
                    fi = new System.IO.FileInfo(FilePath);
                    FileLastWriteDateS = "LU:" + fi.LastWriteTime.ToShortTimeString();
                } catch (Exception e) {
                    Debug.Print("An exception was thrown:{0}\n{1}\n{2}", e.ToString(), e.Message, e.StackTrace);
                }

            }
            */

            if (File_ShowPreview == true) {
                // プレビューを読み込む程度
                string ExtS = TempFD.FileExt;
                StringBuilder SB = new StringBuilder();

                // 最終更新時刻を個別で取得するようになったのでCO
                // int ReadChar = 100 - FileLastWriteDateS.Length; //現在時刻ぶん
               // int ReadChar = 100; //現在時刻ぶん

                char[] ReadBuff = new char[100];
                //for (int i = 0; i < FileListS.Count; i++) {
                //    FileStream FS = new FileStream(CurrentFD.FilePath,);

                if (ExtS == ".txt") {
                    PreviewedStringS = "";
                    //100文字読みたい *Async*
                    try {
                        using (StreamReader SR = new StreamReader(FilePath, Encoding.Default)) {
                            ReadBuff = new char[100];
                            await SR.ReadAsync(ReadBuff, 0, 100);
                        }
                    } catch (Exception e) {
                        Debug.Print("An exception was thrown:{0}\n{1}\n{2}", e.ToString(), e.Message, e.StackTrace);
                    }

                    foreach (char c in ReadBuff) {
                        if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)) {
                            SB.Append(c);
                        }
                    }
                    PreviewedStringS = SB.ToString();
                }
            }


            TempFD.FilePreviewS = PreviewedStringS;
            FileListS[Index] = TempFD;

            //Debug.Print("Completed:returned {0}",PreviewedStringS);
            return PreviewedStringS;
            //}
        }

        private void MainBox_LocationChanged(object sender, EventArgs e) {
            MenuPic.Location = new Point(Width - 85, Height - 110);
            DDLabel.Location = new Point(( this.Width - DDLabel.Size.Width - 6 ) / 2, ( this.Height - DDLabel.Size.Height - 24 ) / 2);
            DisposeBox.Location = new Point(Width / 2 - 25, 10);
        }

        private void MainBox_SizeChanged(object sender, EventArgs e) {
            MenuPic.Location = new Point(Width - 85, Height - 110);
            DDLabel.Location = new Point(( this.Width - DDLabel.Size.Width - 6 ) / 2, ( this.Height - DDLabel.Size.Height - 24 ) / 2);
            DisposeBox.Location = new Point(Width / 2 - 25, 10);
        }

        private void MainBox_Load(object sender, EventArgs e) {
            DragUpdateTimer.Interval = DragUpdateTick;
            LoadSettings();
            AutoHideTimer.Interval = AutoHideTime * 1000;
            TopMost = TopWindow;
        }

        private void InfoUpdateTimer_Tick(object sender, EventArgs e) {

        }

        private void NotiCloseTimer_Tick(object sender, EventArgs e) {
            if (NotiProductionMode == ProductionModeE.Wait) {
                NotiProductionTimer.Start();
                NotiCloseTimer.Stop();
                NotiProductionMode = ProductionModeE.Leave;
            }
        }

        //下から通知が上がってくるシステム
        private void NotiProductionTimer_Tick(object sender, EventArgs e) {
            //Debug.Print("{0}",InfoLoopCount);
            bool WillContinue = true;
            switch (NotiProductionMode) {
                case ProductionModeE.None:
                WillContinue = false;
                break;

                case ProductionModeE.Enter:
                InfoLoopCount++;
                if (InfoLoopCount == InfoMaxLoopCount) {
                    NotiProductionMode = ProductionModeE.Wait;
                }
                break;

                case ProductionModeE.Leave:
                InfoLoopCount--;
                if (InfoLoopCount == 0) {
                    NotiProductionMode = ProductionModeE.None;
                    WillContinue = false;
                    // 終わったので消してみる
                    foreach (string Name in InfoUsingControls) {
                        NotiTLP.Controls.Clear();
                        AdditionalControls.Remove(Name);
                        this.Controls.RemoveByKey(Name);
                    }
                    InfoUsingControls.Clear();
                    WillContinue = false;
                }
                break;

                case ProductionModeE.Wait:
                    //待つので何もしない
                break;

            }

            if (WillContinue == true) {
                NotiProductionTimer.Start();
            } else {
                NotiProductionTimer.Stop();
            }

            SaveNotiTLP.Location = new Point(0, (int)(this.Height - (((double)InfoLoopCount) / (double)InfoMaxLoopCount) * 26 - SystemInformation.CaptionHeight - 16));
            //Debug.Print("{0}", (int)( this.Height - ( ( (double)InfoLoopCount ) / (double)InfoMaxLoopCount ) * 26 - SystemInformation.CaptionHeight - 16 ));
        }

        /// <summary>
        /// 下の方からスライドしてくる通知を表示します。
        /// 通知と一緒に表示するボタンなどは、配列を使用してください。
        /// </summary>
        /// <param name="ObjectTLP">通知として扱う TableLayoutPanel コントロール。
        ///                         座標/サイズが可変である必要があります。</param>
        /// <param name="UsedControls">AdditionalControls に追加した、通知として使用しているコントロールの名前が代入された配列。</param>
        /// <param name="TimeSec">通知を表示する秒数。</param>

        public void ShowInfo(TableLayoutPanel ObjectTLP,List<String> UsedControls,int TimeSec) {
            NotiCloseTimer.Stop();
            NotiProductionTimer.Stop();
            NotiProductionMode = ProductionModeE.Enter;
            InfoLoopCount = 0;
            //サイズ設定
            ObjectTLP.Location = new System.Drawing.Point(0, this.Height - 26);
            ObjectTLP.Size = new System.Drawing.Size(this.Width, 26);
            ObjectTLP.Visible = true;
            //自動的に閉じてくれそうなタイマー
            NotiCloseTimer.Interval = TimeSec * 1000;
            NotiCloseTimer.Start();
            //演出用
            NotiProductionMode = ProductionModeE.Enter;
            NotiProductionTimer.Start();
            //参照を残す
            SaveNotiTLP = ObjectTLP;
            //リソースを開放できるようにリストを保持しておく
            InfoUsingControls = UsedControls;
        }

        public bool OpenSettings() {
            if (IsSettingsOpenB == false) { // 設定が開いていないか
                WF_FileDropBoard.Setting ST = new WF_FileDropBoard.Setting(this);
                ST.Show();
                //設定ウィンドウの位置決定(ずれたところに)
                Point PT = this.Location;
                PT.X += 30;
                PT.Y += 30;
                ST.Location = PT;
                this.TopMost = false; //最前面解除
                IsSettingsOpenB = true; //設定を開いていることにする
                ReShowIntvTimer.Stop();
                AutoHideTimer.Stop(); //隠さないようにする
                return true;
            } else {
                return false;
            }
            //return false;
        }

        /// <summary>
        /// エラーログを追加します。
        /// </summary>
        /// <param name="ErrorMessage">エラーとなるメッセージ。</param>
        /// <param name="NotifyFlag">ユーザーに通知するか。</param>
        public void AddError(string ErrorMessage,bool NotifyFlag) {
            Logs.Add("[ERROR]" + ErrorMessage);
        }

        public void LoadSettings() {
            if (File.Exists(FilePath + FileName)) {
                DataIO dataIO = new DataIO();
                Configuration CF = DataIO.LoadSettings(FilePath + FileName);
                CF.MB = this;
                CF.ExportSettings();
            } else {
                Directory.CreateDirectory( FilePath );
            }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e) {
            this.Exit();
        }

        public void Exit() {
            SaveConfiguration(this);
            Application.Exit();
        }

        public void SaveConfiguration(Main MB) {
            //コントロールの構成
            // --------------------------------
            // | DescLB                        |
            // --------------------------------
            //イメージ的には......
            Label DescLB = new Label() {
                Text = "保存しています...",
                AutoSize = false,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Name = "NT_SavingDescLabel",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            //通知のパネルの背景色をいじる (これを白にすると見えなくて困る)
            MB.NotiTLP.BackColor = MB.Noti_InfoColor;
            //座標とサイズをいじる
            MB.NotiTLP.Location = new Point(0, this.Height);
            MB.NotiTLP.Size = new Size(Width, 30);
            //列を初期化して追加する
            MB.NotiTLP.ColumnStyles.Clear();
            MB.NotiTLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            //コントロールを追加する
            MB.NotiTLP.Controls.Clear();
            MB.NotiTLP.Controls.Add(DescLB, 0, 0);
            //使用しているコントロールの名前を設定する
            //これがないとリソースを開放できない
            List<string> UseControls = new List<string> {
                    "NT_SavingDescLabel",
            };
            //表示する準備
            MB.NotiTLP.BringToFront();
            MB.NotiTLP.Visible = true;
            //通知として初期化する
            MB.ShowInfo(MB.NotiTLP, UseControls, 999);
            //保存開始
            MB.IsSettingsOpenB = false;
            Configuration CF = new Configuration(MB);
            DataIO dataIO = new DataIO();
            CF.ImportSettings();
            string SaveMessage = "設定を保存しました";
            //バックアップ
            try {
                File.Copy(MB.FilePath + MB.FileName, MB.FilePath + MB.FileName + ".old", true);
            } catch (Exception) {

            }

             try {
            DataIO.SaveSettings(MB.FilePath + MB.FileName, CF);
             } catch (Exception e3) {
            SaveMessage = String.Format("設定は保存されませんでした({0})", e3.Message);
                MessageBox.Show(e3.Message);
             }

            //無理やり通知を閉じる
            MB.NotiCloseTimer.Stop();
            MB.NotiCloseTimer.Interval = 1;
            MB.NotiCloseTimer.Start();
            DescLB.Dispose();

            //コントロールの構成
            // --------------------------------
            // | DescLB                        |
            // --------------------------------
            //イメージ的には......
            Label DescLB2 = new Label() {
                Text = SaveMessage,
                AutoSize = false,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Name = "NT_SavedDescLabel",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill
            };
            //通知のパネルの背景色をいじる (これを白にすると見えなくて困る)
            MB.NotiTLP.BackColor = MB.Noti_SuccColor;
            //座標とサイズをいじる
            MB.NotiTLP.Location = new Point(0, this.Height);
            MB.NotiTLP.Size = new Size(Width, 30);
            //列を初期化して追加する
            MB.NotiTLP.ColumnStyles.Clear();
            MB.NotiTLP.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            //コントロールを追加する
            MB.NotiTLP.Controls.Clear();
            MB.NotiTLP.Controls.Add(DescLB2, 0, 0);
            //使用しているコントロールの名前を設定する
            //これがないとリソースを開放できない
            List<string> UseControls2 = new List<string> {
                    "NT_SavedDescLabel",
            };
            //表示する準備
            MB.NotiTLP.BringToFront();
            MB.NotiTLP.Visible = true;
            //通知として初期化する
            MB.ShowInfo(MB.NotiTLP, UseControls2, 3);
            System.Media.SystemSounds.Asterisk.Play();
            DescLB.Dispose();
        }

        private void AutoHideTimer_Tick(object sender, EventArgs e) {
            if (Focused == true && IsSettingsOpenB == false) { //設定が開いていないなら かつ フォーカスがあるなら
                ReShowIntvTimer.Start(); //再表示判定を起動する
                this.Visible = false; //非表示にする

                // 再出現の位置を決める
                Point Loc = this.Location;
                RectangleF wa = Screen.PrimaryScreen.WorkingArea;

                switch (AutoHidePosition) {
                    case AutoHidePositionE.UpperLeft:
                        Loc.X = 10;
                        Loc.Y = 10;
                        break;

                    case AutoHidePositionE.UnderLeft:
                        Loc.X = 10;
                        Loc.Y = (int)wa.Height - this.Height - 10;
                        break;

                    case AutoHidePositionE.UpperRight:
                        Loc.X = (int)wa.Width - this.Width - 10;
                        Loc.Y = 10;
                        break;

                    case AutoHidePositionE.UnderRight:
                        Loc.X = (int)wa.Width - this.Width - 10;
                        Loc.Y = (int)wa.Height - this.Height - 10;
                        break;
                }

                this.Location = Loc;
            }
        }

        private void ReShowIntvTimer_Tick(object sender, EventArgs e) {
            //再出現の判定
            bool IsCursorIn = false; //カーソルが判定枠に入ったか
            Point cp = Cursor.Position;
            RectangleF wa = Screen.PrimaryScreen.WorkingArea;
            //無理やりだけど判定させる
            switch (AutoHidePosition) {

                case AutoHidePositionE.UpperLeft: //左上隅の場合
                    if ( (cp.X < ( this.Width + 10)) &&
                         (cp.Y < ( this.Height + 10))
                       ) {
                        IsCursorIn = true;
                    }
                break;

                case AutoHidePositionE.UnderLeft: //左下隅の場合
                    if (( cp.X < ( this.Width + 10 ) ) &&
                         ( cp.Y > ( wa.Height - this.Height - 10 ) )
                       ) {
                        IsCursorIn = true;
                    }
                break;

                case AutoHidePositionE.UpperRight: //右上隅の場合
                    if (( cp.X > ( wa.Width - this.Width - 10 ) ) &&
                        ( cp.Y < ( this.Height + 10 ) )
                        ) {
                        IsCursorIn = true;
                    }
                    break;

                case AutoHidePositionE.UnderRight: //右下隅の場合
                    if ( (cp.X > (wa.Width - this.Width - 10 )) &&
                         (cp.Y > (wa.Height - this.Height - 10 ))  
                       ) {
                        IsCursorIn = true; 
                    }
                break;

            }

            if (IsCursorIn) { //カーソルが判定枠の中に入っていた
                //再表示する
                AutoHideTimer.Stop();
                ReShowIntvTimer.Stop();
                this.Visible = true;
                this.TopMost = TopWindow;
                this.Activate(); //フォーカスをもらう
            } else {
                //入ってなかった
                ReShowIntvTimer.Start(); //判定まで待つ
            }
        }


    }
}

// By the way, do you like PINEAPPLES? I love it! xD
// [EOF]
