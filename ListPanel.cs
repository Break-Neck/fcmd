﻿/* The File Commander
 * Элемент управления ListPanel (улучшенный аналог ListView)
 * (C) 2013, Alexander Tauenis (atauenis@yandex.ru)
 * Копирование кода разрешается только с письменного согласия
 * разработчика (А.Т.).
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace fcmd
{
    public partial class ListPanel : System.Windows.Forms.UserControl
    {
		//Типы
		public class ItemDescription{//тип для пункта в списке
			public List<string> Text = new List<string>();
			public string Value;
			public bool Selected = false;
			public short Selection = 0;
			public Image Icon;
		}
		public struct CollumnOptions{//параметры столбца
			/// <summary>
			/// Заголовок столбца ///
			/// The collumn's caption.
			/// </summary>
			public string Caption;

			/// <summary>
			/// Метка слобца (для определения что это такое) ///
			/// The collumn's tag.
			/// </summary>
			public string Tag;

			/// <summary>
			/// Ширина и высота столбца ///
			/// The collumn's size.
			/// </summary>
			public System.Drawing.Size Size;
		}

		//Внутренние переменные
		pluginner.IFSPlugin FsPlugin;

		//Подпрограммы
        public ListPanel(){//Ну, за инициализацию!
			InitializeComponent();
            list.GotFocus += (sender, e) => OnGotFocus(e);
			list.LostFocus += (sender, e) => OnLostFocus(e);
        }

		public new event StringEvent DoubleClick;

        private void ListPanel_Load(object sender, EventArgs e){//Ну, за загрузку!
            Repaint();
			this.MouseClick += (snd, ea) => this.Focus();
        }

        //Свойства
		/// <summary>
		/// Gets or sets the file system driver.
		/// </summary>
		/// <value>
		/// The file system driver.
		/// </value>
		public pluginner.IFSPlugin FSProvider{
			get{return FsPlugin;}
			set{FsPlugin = value;}
		}

        private void list_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = list.SelectedItems[0].Text + "      " + list.SelectedItems[0].SubItems[1].Text;
            }
            catch (System.ArgumentOutOfRangeException aoore)
            {
                //всё нормально, пользователь не выбрал никакого пункта
                lblStatus.Text = " ";//пустая строка чтобы не было неприятностей с работой AutoSize
            }
            catch (Exception ex) { lblStatus.Text = ex.Message; }
        }

        /// <summary>
        /// Repaint the ListPanel
        /// </summary>
        public void Repaint()
        {
            lblStatus.Visible = fcmd.Properties.Settings.Default.ShowFileInfo;
            foreach(string d in System.IO.Directory.GetLogicalDrives()){
                ToolStripButton tsb = new ToolStripButton(d,null,(object s, EventArgs ea) => {ToolStripButton tsbn = (ToolStripButton)s; LP_goToDir("file://" + tsbn.Text);});
                tsDisks.Items.Add(tsb);
            }
        }

        /// <summary>
        /// Load the directory <paramref name="url"/>. To be used only inside listpanel.cs
        /// </summary>
        /// <param name="url"></param>
        private void LP_goToDir(string url)
        {
            FsPlugin.CurrentDirectory = url;
            list.Items.Clear();
            foreach (pluginner.DirItem di in FsPlugin.DirectoryContent)
            {
                if (di.Hidden == false)
                {
                    ListViewItem NewItem = new ListViewItem(di.TextToShow);
                    NewItem.Tag = di.Path; //путь будет тегом
                    NewItem.SubItems.Add(Convert.ToString(di.Size / 1024) + "KB");
                    NewItem.SubItems.Add(di.Date.ToShortDateString());
                    list.Items.Add(NewItem);
                }
            }
        }
    }
}
