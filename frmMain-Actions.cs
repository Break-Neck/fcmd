﻿/* The File Commander - main window
 * The main window backend.
 * (C) The File Commander Team - https://github.com/atauenis/fcmd
 * (C) 2013, Alexander Tauenis (atauenis@yandex.ru)
 * Contributors should place own signs here.
 */
using System;
using System.Windows.Forms;//убрать!
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;

namespace fcmd
{
    public partial class frmMain
    {
        /* ЗАМЕТКА РАЗРАБОТЧИКУ
         * 
         * В данном файле размещаются комманды "коммандной строки" FC, которые также
         * используются в штатных функциях файлового менеджера (навигация по каталогам).
         * Все комманды работают с активной и пассивой панелью - Active/PassivePanel.
         * FC всегда их определяет сам. Пассивая панель - всегда получатель файлов.
         * Названия комманд - UNIX в верблюжьем регистре (Ls, Rm, MkDir, Touch и т.п.).
         * Всем коммандам параметры передаются строкой, но допускаются исключения, напр.,
         * если базовая функция "перегружена" функцией для нужд графического интерфейса.
         */

        /// <summary>
        /// Reads the <paramref name="url"/>'s file/subdir list and pushes it into the active panel.
        /// </summary>
        /// <param name="url"></param>
        public void Ls(string url)
        {
            int Status = 0;
            ActivePanel.lblPath.Text = url;
            Thread LsThread = new Thread(delegate() { DoLs(url, ActivePanel, ref Status); });
            FileProcessDialog fpd = new FileProcessDialog();
            fpd.Location = new Xwt.Point(this.Top + ActivePanel.Top, this.Left + ActivePanel.Left);
            string FPDtext = String.Format(locale.GetString("DoingListdir"), "\n" + url, "");
            FPDtext = FPDtext.Replace("{1}", "");
            fpd.lblStatus.Text = FPDtext;

            fpd.Show();
            LsThread.Start();

            do
            {
                Application.DoEvents();
                fpd.pbrProgress.Fraction = Status;
            }
            while (LsThread.ThreadState == ThreadState.Running);
            fpd.Hide();
        }

        /// <summary>
        /// Reads the file <paramref name="url"/> and shows in FC Viewer
        /// </summary>
        /// <param name="url"></param>
        public void FCView(string url)
        {
            VEd fcv = new VEd();
            pluginner.IFSPlugin fs = ActivePanel.FSProvider;
            if (!fs.FileExists(url))
            {
                //MessageBox.Show(string.Format(locale.GetString("FileNotFound"), ActivePanel.list.SelectedItems[0].Text), "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                new MsgBox(string.Format(locale.GetString("FileNotFound")), ActivePanel.list.SelectedItems[0].Text, MsgBox.MsgBoxType.Warning);
                return;
            }

            pluginner.File SelectedFile = fs.GetFile(url, new int());
            string FileContent = Encoding.ASCII.GetString(SelectedFile.Content);
            fcv.LoadFile(url, ActivePanel.FSProvider, false);
            fcv.Show();
        }

        /// <summary>
        /// Reads the file <paramref name="url"/> and returns it as string 
        /// (for File Commander's CLI panel)
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public string Cat(string url)
        {
            fcview fcv = new fcview();
            pluginner.IFSPlugin fs = ActivePanel.FSProvider;
            if (!fs.FileExists(url)) return "Файл не найден\n";

            pluginner.File SelectedFile = fs.GetFile(url, new int());
            return Encoding.ASCII.GetString(SelectedFile.Content);
        }

        /// <summary>
        /// Makes a new directory at the specifed <paramref name="url"/>
        /// </summary>
        /// <param name="url"></param>
        public void MkDir(string url)
        {
            FileProcessDialog fpd = new FileProcessDialog();
            fpd.lblStatus.Text = string.Format(locale.GetString("DoingMkdir"), "\n" + url, null);
            fpd.Show();

            Thread MkDirThread = new Thread(delegate() { ActivePanel.FSProvider.CreateDirectory(url); });
            MkDirThread.Start();

            LoadDir(ActivePanel.FSProvider.CurrentDirectory, ActivePanel);

            do { Application.DoEvents(); }
            while (MkDirThread.ThreadState == ThreadState.Running);

            fpd.pbrProgress.Fraction = 100;
            LoadDir(ActivePanel.FSProvider.CurrentDirectory, ActivePanel);
            fpd.Hide();
        }

        /// <summary>
        /// Removes the specifed file
        /// </summary>
        /// <param name="url"></param>
        public string Rm(string url)
        {
            if (!Xwt.MessageDialog.Confirm(
                String.Format(locale.GetString("FCDelAsk"), url, null),
                Xwt.Command.Remove,
                false))
            { return locale.GetString("Canceled"); };

            FileProcessDialog fpd = new FileProcessDialog();
            fpd.lblStatus.Text = String.Format(locale.GetString("DoingRemove"), "\n" + url, null);
            fpd.cmdCancel.Sensitive = false;
            fpd.Show();

            string curItemDel = ActivePanel.list.SelectedItems[0].Tag.ToString();
            pluginner.IFSPlugin fsdel = ActivePanel.FSProvider;
            if (fsdel.FileExists(curItemDel))
            {
                fpd.pbrProgress.Fraction = 50;
                Thread RmFileThread = new Thread(delegate() { DoRmFile(curItemDel, fsdel); });
                RmFileThread.Start();

                do { Application.DoEvents(); }
                while (RmFileThread.ThreadState == ThreadState.Running);

                fpd.pbrProgress.Fraction = 100;
                LoadDir(ActivePanel.FSProvider.CurrentDirectory, ActivePanel);
                fpd.Hide();
                return "Файл удалён.\n";
            }
            if (fsdel.DirectoryExists(curItemDel))
            {
                fpd.lblStatus.Text = String.Format(locale.GetString("DoingRemove"), "\n" + url, "\n[" + locale.GetString("Directory").ToUpper() + "]");
                fpd.pbrProgress.Fraction = 0.5;
                Thread RmDirThread = new Thread(delegate() { DoRmDir(curItemDel, fsdel); });
                RmDirThread.Start();

                do { Application.DoEvents(); }
                while (RmDirThread.ThreadState == ThreadState.Running);

                fpd.pbrProgress.Fraction = 1;
                LoadDir(ActivePanel.FSProvider.CurrentDirectory, ActivePanel);
                fpd.Hide();
                return "Каталог удалён.\n";
            }
            return "Файл не найден";
        }

        /// <summary>
        /// Copy the highlighted file to the passive panel. To be used in FC UI. 
        /// Includes asking of the destination path.
        /// </summary>
        public void Cp()
        {
            int Progress = 0;
            string SourceURL = ActivePanel.list.SelectedItems[0].Tag.ToString();
            pluginner.IFSPlugin SourceFS = ActivePanel.FSProvider;
            pluginner.File SourceFile = SourceFS.GetFile(SourceURL, Progress);

            if (!SourceFS.FileExists(SourceURL))
            {//такого файла (уже?) нет
                if (SourceFS.DirectoryExists(SourceURL))//а вдруг есть такой каталог?
                {
                    InputBox ibxd = new InputBox(String.Format(locale.GetString("CopyTo"), SourceFile.Name), PassivePanel.FSProvider.CurrentDirectory + "/" + SourceFile.Name);

                    if (ibxd.ShowDialog())
                    {
                        //копирование каталога
                        Thread CpDirThread = new Thread(delegate() { DoCpDir(SourceURL, ibxd.Result); });

                        FileProcessDialog CpDirProgressDialog = new FileProcessDialog();
                        CpDirProgressDialog.Y = this.Top + ActivePanel.Top;
                        CpDirProgressDialog.X = this.Left + ActivePanel.Left;
                        CpDirProgressDialog.lblStatus.Text = String.Format(locale.GetString("DoingCopy"), "\n" + ActivePanel.list.SelectedItems[0].Tag.ToString() + " [" + locale.GetString("Directory") + "]\n", ibxd.Result, null);
                        CpDirProgressDialog.cmdCancel.Clicked += (object s, EventArgs e) => { CpDirThread.Abort(); new MsgBox(locale.GetString("Canceled"), ActivePanel.list.SelectedItems[0].Tag.ToString(), MsgBox.MsgBoxType.Warning); };

                        CpDirProgressDialog.Show();
                        CpDirThread.Start();

                        do { Application.DoEvents(); }
                        while (CpDirThread.ThreadState == ThreadState.Running);

                        LoadDir(PassivePanel.FSProvider.CurrentDirectory, PassivePanel); //обновление пассивной панели
                        CpDirProgressDialog.Hide();
                    }
                    return;
                }
                MessageBox.Show(string.Format(locale.GetString("FileNotFound"), ActivePanel.list.SelectedItems[0].Text), "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }

            InputBox ibx = new InputBox(String.Format(locale.GetString("CopyTo"), SourceFile.Name), PassivePanel.FSProvider.CurrentDirectory + "/" + SourceFile.Name);
            if (ibx.ShowDialog())
            {

                Thread CpThread = new Thread(delegate() { DoCp(ActivePanel, PassivePanel, ibx.Result, SourceFile, Progress); });
                FileProcessDialog fpd = new FileProcessDialog();
                fpd.Y = this.Top + ActivePanel.Top;
                fpd.X = this.Left + ActivePanel.Left;
                fpd.lblStatus.Text = String.Format(locale.GetString("DoingCopy"), "\n" + ActivePanel.list.SelectedItems[0].Tag.ToString() + "\n", ibx.Result, null);
                fpd.cmdCancel.Clicked += (object s, EventArgs e) => { CpThread.Abort(); new MsgBox(locale.GetString("Canceled"), ActivePanel.list.SelectedItems[0].Tag.ToString(), MsgBox.MsgBoxType.Warning); };

                CpThread.Start();
                fpd.Show();

                do { Application.DoEvents(); fpd.pbrProgress.Fraction = Progress; }
                while (CpThread.ThreadState == ThreadState.Running);

                LoadDir(PassivePanel.FSProvider.CurrentDirectory, PassivePanel); //обновление пассивной панели
                fpd.Hide();
            }
            else return;
        }

        /// <summary>
        /// Move the selected file or directory
        /// </summary>
        void Mv()
        {
            //Getting the handles of the source and the destination filesystems
            pluginner.IFSPlugin SourceFS = ActivePanel.FSProvider;
            pluginner.IFSPlugin DestinationFS = PassivePanel.FSProvider;

            //Getting useful URL parts
            string SourceName = ActivePanel.list.SelectedItems[0].SubItems[0].Text;
            string SourcePath = ActivePanel.list.SelectedItems[0].Tag.ToString();
            string DestinationPath = DestinationFS.CurrentDirectory + SourceFS.DirSeparator + SourceName;

            //Giving the user the one chance to change the destination path
            InputBox ibx = new InputBox
                (
                string.Format(locale.GetString("MoveTo"), SourceName),
                DestinationPath
                );
            if (ibx.ShowDialog())
                DestinationPath = ibx.Result;
            else return;

            // Comparing the filesystems; if they is diffrent, use copy&delete
            // instead of direct move (if anyone knows, how a file can be moved
            // from an FTP to an ext2fs on Windows or MacOS please tell me :-) )
            if (SourceFS.GetType() != DestinationFS.GetType())
            {
                Cp();
                //MessageBox.Show("Cannot move between diffrent filesystems!\nНе сделана поддержка перепещения между разными ФС");
                Xwt.MessageDialog.ShowError("Cannot move between diffrent filesystems!\nНе сделана поддержка перепещения между разными ФС");
                return;
            }

            //Now, assuming that the src & dest fs is same and supports
            //cross-disk file moving
            if (SourceFS.DirectoryExists(SourcePath))
            {//this is a directory
                SourceFS.MoveDirectory(SourcePath, DestinationPath);
            }
            if (SourceFS.FileExists(SourcePath))
            {//this is a file
                SourceFS.MoveFile(SourcePath, DestinationPath);
            }


            //Final clean-up and UI refresh
            GC.Collect(); //needs to find that it is really is need
            LoadDir(ActivePanel.FSProvider.CurrentDirectory, ActivePanel);
            LoadDir(PassivePanel.FSProvider.CurrentDirectory, PassivePanel);
        }

    }
}