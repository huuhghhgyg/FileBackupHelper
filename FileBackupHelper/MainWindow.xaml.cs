using FileBackupHelper.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FileBackupHelper
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //读取配置文件
            ReadSettings();

            if (AutoBackupChecker.IsChecked == true)
            {
                if (Directory.Exists(FinalDirectory.Text) == true)
                {//如果目的目录存在就开始备份
                    Thread BackupThread = new Thread(StartBackupFile);
                    BackupThread.Start();
                }
                else
                {//如果目的目录不存在就提示
                    MessageBox.Show("目的目录不存在，请填写有效目录", "无法开始备份(自动)");
                }
            }
        }

        void ReadSettings()//读取设置
        {
            string _list = Settings.Default["FromDirectoryList"].ToString();//构造列表
            string _listcahce = _list;
            while (_list.ToString().IndexOfAny("@".ToArray()) != -1)//如果有@
            {
                _list = _list.Substring(0, _listcahce.LastIndexOf("@"));//去除最后一条
                BackupList.Items.Add(_listcahce.Substring(_listcahce.LastIndexOf("@") + 1));//将最后一条添加至列表（获取最后一条）
                _listcahce = _list;//恢复cache
            }

            FinalDirectory.Text = Settings.Default["ToDirectory"].ToString();//恢复目的目录

            AutoBackupChecker.IsChecked = bool.Parse(Settings.Default["AutoBackup"].ToString());
        }

        private void MoreInfoButton_Collapsed(object sender, RoutedEventArgs e)//收缩事件
        {
            Window.Height = 200 - 56 + 10;
        }

        private void MoreInfoButton_Expanded(object sender, RoutedEventArgs e)//扩张事件
        {
            Window.Height = 220;
        }

        private void DelButton_Click(object sender, RoutedEventArgs e)//删除选中item按钮
        {
            BackupList.Items.Remove(BackupList.SelectedItem);
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)//将textbox中的内容添加至列表
        {
            string StringItem = AddItemText.Text;//(字符串)要添加的路径
            if (Directory.Exists(StringItem) || File.Exists(StringItem))
            {
                StringItem.Replace("/", "\\");//原始字符，替换后字符(格式化路径)
                string HardDiskLetter = AddItemText.Text.Substring(0, 1).ToUpper();//首字母
                StringItem = StringItem.Substring(1);//去除盘符
                StringItem = HardDiskLetter + StringItem;//替换盘符
                BackupList.Items.Add(StringItem);//如果有目录，添加
                AddItemText.Text = "";//清空添加框
            }
            else
            {
                MessageBox.Show("指定目录不存在", "添加失败");//没有则报错
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(FinalDirectory.Text) == true)
            {//如果目的目录存在就开始备份
                Thread BackupThread = new Thread(StartBackupFile);
                BackupThread.Start();
            }
            else
            {//如果目的目录不存在就提示
                MessageBox.Show("目的目录不存在，请填写有效目录", "无法开始备份");
            }
        }

        int FileNumber = 0;//文件总数量,设置了初始值为0
        void StartBackupFile()
        {
            //UI设置
            Dispatcher.Invoke(new Action(delegate
            {
                BackupStateTitle.Text = "正在备份";
                FinalDirectory.IsEnabled = false;
                //(设置progressbarMaxvalue的值)
                StartButton.IsEnabled = false;
                Settings.Default["ToDirectory"] = FinalDirectory.Text;//保存目的目录(代码)
                Settings.Default["AutoBackup"] = AutoBackupChecker.IsChecked;//保存自动备份状态
                Settings.Default.Save();
            }));

            //代码
            string BackupFileList = "";//备份列表 (@File@File2@File3)
            foreach (object each in BackupList.Items)//递归计算文件总数
            {
                BackupFileList += "@" + each.ToString();//添加列表
                FileNumber += GetFilesCount(each.ToString());
            }
            Settings.Default["FromDirectoryList"] = BackupFileList;
            Settings.Default.Save();//保存配置文件
            string FinalDirectory_str = "";//(cache)目的目录
            Dispatcher.Invoke(new Action(delegate
            {
                ProgressBar.Maximum = FileNumber;//将进度条的最大值设置为文件总个数
                FinalDirectory_str = FinalDirectory.Text;
            }));
            foreach (object each in BackupList.Items)
            {
                CopyDirectory(each.ToString(), FinalDirectory_str);//从列表上的文件到备份目录
            }

            //UI还原
            Dispatcher.Invoke(new Action(delegate
            {
                BackupStateTitle.Text = "备份完成";//设置标题
                FinalDirectory.IsEnabled = true;//还原 备份到 目录
                StartButton.IsEnabled = true;//还原备份按钮
                ProgressBar.Value = 0;//进度条还原
            }));
        }

        int GetFilesCount(string path)//获取文件总个数
        {
            int count = 0;
            //如果嵌套文件夹很多，可以开子线程去统计
            count += Directory.GetFiles(path).Length;
            foreach (var folder in Directory.GetDirectories(path))
            {
                count += GetFilesCount(folder);
            }
            return count;
        }

        int CopiedFileNumber = 0;//已备份文件个数
        int FailedNumber = 0;//备份失败文件个数
        private void CopyDirectory(string fromdir, string todir)//复制文件(方法)，顺便更新states和进度条
        {
            string folderName = fromdir.Substring(fromdir.LastIndexOf("\\") + 1);//获取要备份的目录你的名字

            string desfolderdir = todir + "\\" + folderName;//获取最终目录的路径

            if (todir.LastIndexOf("\\") == (todir.Length - 1))
            {
                desfolderdir = todir + folderName;
            }
            string[] filenames = Directory.GetFileSystemEntries(fromdir);

            foreach (string file in filenames)// 遍历所有的文件和目录
            {
                if (Directory.Exists(file))// 先当作目录处理如果存在这个目录就递归Copy该目录下面的文件
                {

                    string currentdir = desfolderdir + "\\" + file.Substring(file.LastIndexOf("\\") + 1);
                    if (!Directory.Exists(currentdir))
                    {
                        try
                        {
                            Directory.CreateDirectory(currentdir);
                        }
                        catch (DirectoryNotFoundException)
                        {
                            Dispatcher.Invoke(new Action(delegate
                            {
                                AutoBackupChecker.IsChecked = false;
                                //saveSettings();
                            }));
                            Environment.Exit(0);
                        }
                    }

                    Dispatcher.Invoke(new Action(delegate
                    {
                        //backuppath.Content = pathList.Items[backupNums].ToString();
                        //backupName.Content = file;

                        UpdateBackupStates(file);//更新备份状态
                    }));
                    CopyDirectory(file, desfolderdir);
                }

                else // 否则直接copy文件
                {
                    string srcfileName = file.Substring(file.LastIndexOf("\\") + 1);

                    srcfileName = desfolderdir + "\\" + srcfileName;


                    if (!Directory.Exists(desfolderdir))
                    {
                        Directory.CreateDirectory(desfolderdir);
                    }

                    Dispatcher.Invoke(new Action(delegate
                    {
                        //backuppath.Content = BackupList.Items[backupNums].ToString();
                        //backupName.Content = file;
                        UpdateBackupStates(file);//更新备份状态
                    }));

                    CopiedFileNumber++;
                    Dispatcher.Invoke(new Action(delegate
                    {
                        ProgressBar.Value = CopiedFileNumber;

                        if (ProgressBar.Value == ProgressBar.Maximum)
                        {
                            BackupStateTitle.Text = "备份完成";

                            BackupStatesBox.Text = "完成!\n已备份文件个数:" + CopiedFileNumber.ToString() + "/" + FileNumber.ToString()
                            + "\n备份失败文件个数:" + FailedNumber.ToString();
                        }
                        //backuppath.Content = srcfileName;
                        //backupName.Content = file;
                        //backupNum.Content = (CopiedFileNumber + "/" + fileNum);
                        UpdateBackupStates(file);//更新状态
                    }));
                    try
                    {
                        File.Copy(file, srcfileName);
                    }
                    catch (IOException)
                    {
                        try//important
                        {
                            File.Delete(srcfileName);
                            File.Copy(file, srcfileName);
                        }
                        catch
                        {
                            FailedNumber++;
                        }
                    }

                }
            }//foreach 
        }//function end 


        void UpdateBackupStates(string BackupingFileName)//正在备份的文件名称，备份过的文件个数,备份失败文件个数
        {
            //文件总个数(public FileNumber)
            BackupStatesBox.Text =
                "正在备份:" + BackupingFileName
                + "\n已备份文件个数:" + CopiedFileNumber.ToString() + "/" + FileNumber.ToString()
                + "\n备份失败文件个数:" + FailedNumber.ToString();
        }
    }
}
