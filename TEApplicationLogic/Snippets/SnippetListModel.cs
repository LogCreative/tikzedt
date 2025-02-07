﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;
using System.Windows;
using System.Threading;
//using Ionic.Zip;
using ICSharpCode.SharpZipLib;
using ICSharpCode.SharpZipLib.Zip;
using TEApplicationLogic.Snippets;

namespace TikzEdt.Snippets
{

    public class InsertEventArgs : EventArgs
    {
        public string code = "", dependencies = "";
    }

    public class UseStylesEventArgs : EventArgs
    {
        public string nodestyle = "", edgestyle = "", dependencies = "";
        /// <summary>
        /// Indicates whether the style should be used in addition to the ones present
        /// </summary>
        public bool InAddition = false;
    }        

    public interface ISnippetListView
    {
        void Refresh();

        void RaiseOnInsert(InsertEventArgs e);
        void RaiseOnUseStyle(UseStylesEventArgs e);
    }

    public class SnippetListModel
    {

        ISnippetListView TheView;

        public SnippetsDataSet snippetsDataSet { get; private set; }
        public SnippetsDataSet.SnippetsTableDataTable snippetsTable { get; private set; }



        public void CompileSnippets()
        {
            if (GlobalUI.UI.ShowMessageBox("Do you want to create the Snippet Thumbnails now?\r\n" +
    "It may take some time, but it will happen in the background. You can also recompile them later from the menu or the Snippet Manager." +
    "Note: If you are missing some Latex packages, it is better to compile later", "Compile Thumbnails",
    System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            {
                // Compile
                foreach (SnippetsDataSet.SnippetsTableRow r in snippetsTable.Rows)
                {
                    if (!r.IsNull(snippetsTable.SampleCodeColumn))
                    {
                        string cFile = Helper.GetSnippetsPath() + r.ID;
                        TikzToBMPFactory.Instance.AddJob(r.SampleCode, cFile + ".tex", new Rect(0, 0, 0, 0), r.Name, true);
                    }
                }
            }
        }

        public SnippetListModel(ISnippetListView TheView)
        {
            this.TheView = TheView;
            snippetsDataSet = new SnippetsDataSet();
        }

        public void Reload()
        {
			string cSnippetsFile = Path.Combine(Helper.GetSettingsPath(), Consts.cSnippetsFile);
            if (File.Exists( cSnippetsFile ))
            {
                snippetsDataSet.Clear();
                snippetsDataSet.ReadXml(cSnippetsFile);
                snippetsTable = snippetsDataSet.SnippetsTable;

                TheView.Refresh();


                //snippetsTableViewSource.SortDescriptions.Clear();
                //snippetsTableViewSource.SortDescriptions.Add(new System.ComponentModel.SortDescription("Category", System.ComponentModel.ListSortDirection.Ascending));
                //snippetsTableViewSource.SortDescriptions.Add(new System.ComponentModel.SortDescription("Name", System.ComponentModel.ListSortDirection.Ascending));
                //lstSnippets.ItemsSource = from rows in snippetsTable
                //                          orderby rows.Name
                //                          group rows by rows.Category into g
                //                          select g;
                //group rows by rows.Category into g
                //orderby g.Key
                //select g;
            }
        }


        public void CheckForThumbnails()
        {
            // Do Thumbnails exist? -> Unzip or Recompile
            if (!Directory.Exists(Helper.GetSnippetsPath()))
            {
                // first try to unzip snippets
                if ( !UnzipSnippetsMySelf() )
                    CompileSnippets(); // if failed-> ask the user to recompile
            }

        }

        /// <summary>
        /// Unzips the snippets using the zip library, in a new thread.
        /// </summary>
        /// <returns></returns>
        private bool UnzipSnippetsMySelf()
        {
            string zipfile = System.IO.Path.Combine(Helper.GetAppDir(), Consts.cSnippetThumbsZipfile);
            string tgt = Helper.GetAppdataPath() + Path.DirectorySeparatorChar;

            if (!File.Exists(zipfile))
                return false;

            GlobalUI.UI.AddStatusLine(this, "Unzipping snippet thumbnails from file " + zipfile + "....");

            ZipWorker = new TESharedComponents.MyBackgroundWorker();
            ZipWorker.DoWork += (se,e)=>
                {
                    string _zipfile = (e.Argument as Pair<string, string>).First;
                    string _tgt = (e.Argument as Pair<string, string>).Second;
                    try
                    {
                        /* Ionic.Zip code... not working in linux (works fine in windows)
                         * //Console.WriteLine("Unzipping...");
                        using (var z = ZipFile.Read(_zipfile))
                        {
                            z.ExtractAll(_tgt, ExtractExistingFileAction.OverwriteSilently);
                        }
                        */
                        using (ZipInputStream s = new ZipInputStream(File.OpenRead(_zipfile)))
                        {

                            ZipEntry theEntry;
                            while ((theEntry = s.GetNextEntry()) != null)
                            {

                                Console.WriteLine(theEntry.Name);

                                string directoryName = Path.GetDirectoryName(theEntry.Name);
                                string directoryFullPath = Path.Combine(_tgt, directoryName); 
                                string fileName = Path.GetFileName(theEntry.Name);
                                string filePath = Path.Combine(_tgt, theEntry.Name);

                                // create directory
                                if (!Directory.Exists(directoryFullPath))
                                {
                                    Directory.CreateDirectory(directoryFullPath);
                                }

                                if (fileName != String.Empty)
                                {
                                    using (FileStream streamWriter = File.Create(filePath))
                                    {

                                        int size = 2048;
                                        byte[] data = new byte[2048];
                                        while (true)
                                        {
                                            size = s.Read(data, 0, data.Length);
                                            if (size > 0)
                                            {
                                                streamWriter.Write(data, 0, size);
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        GlobalUI.UI.AddStatusLine(null, "Snippet Thimbnails unzipped successfully.");
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine("Couldn't unzip: " + ex.Message);
                        GlobalUI.UI.AddStatusLine(null, "Couldn't unzip snippets thumbnails: " + ex.Message, true);
                    }
                };

            ZipWorker.RunWorkerAsync(new Pair<string,string>(zipfile, tgt));

            return true;
        }
        private TESharedComponents.MyBackgroundWorker ZipWorker;

        /// <summary>
        /// Runs the Unzipper external program (must be present) to unzip
        /// </summary>
        /// <returns>true if Unzipper could be started, false if a problem occurred</returns>
        private bool UnzipSnippetsViaUnzipper()
        {
            string zip = System.IO.Path.Combine(Helper.GetAppDir(), Consts.cSnippetThumbsZipfile);
            string unzipper = System.IO.Path.Combine(Helper.GetAppDir(), Consts.cUnzipper);
            if (File.Exists(zip) && File.Exists(unzipper))
            {
                try
                {
                    GlobalUI.UI.AddStatusLine(this, "Unzipping snippet thumbnails from file " + zip + "....");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        UseShellExecute = false,
                        FileName = unzipper,
                        Arguments = "\"" + zip + "\" \"" + Helper.GetAppdataPath() + "\"",
                        CreateNoWindow = true
                    });

                    return true;
                }
                catch (Exception ex)
                {
                    GlobalUI.UI.AddStatusLine(this, "Unzipping snippet thumbnails failed: " + ex.Message, true);
                    CompileSnippets();  // in case of failure, try to recompile snippets
                }

            }

            return false;
        }


        public void HandleMouseDoubleClick(SnippetsDataSet.SnippetsTableRow r)
        {
            string c = "", d = "";
            if (!r.IsNull(snippetsTable.SnippetCodeColumn))
                c = r.SnippetCode;
            if (!r.IsNull(snippetsTable.DependenciesColumn))
                d = r.Dependencies;

            TheView.RaiseOnInsert(new InsertEventArgs() { code = c, dependencies = d });
        }

        public void HandleInsertSnippetClick(SnippetsDataSet.SnippetsTableRow r)
        {
            string c = "", d = "";
            if (!r.IsNull(snippetsTable.SnippetCodeColumn))
                c = r.SnippetCode;
            if (!r.IsNull(snippetsTable.DependenciesColumn))
                d = r.Dependencies;

            TheView.RaiseOnInsert(new InsertEventArgs() { code = c, dependencies = d });
        }

        public void HandleInsertFullCodeClick(SnippetsDataSet.SnippetsTableRow r)
        {
            string c = "", d = "";
            if (!r.IsNull(snippetsTable.SampleCodeColumn))
                c = r.SampleCode;
            if (!r.IsNull(snippetsTable.DependenciesColumn))
                d = r.Dependencies;

            TheView.RaiseOnInsert(new InsertEventArgs() { code = c, dependencies = d });
        }

        public void HandleInsertDependenciesClick(SnippetsDataSet.SnippetsTableRow r)
        {

            string d = "";

            if (!r.IsNull(snippetsTable.DependenciesColumn))
                d = r.Dependencies;

            TheView.RaiseOnInsert(new InsertEventArgs() { code = @"\usetikzlibrary{" + d + "}" + Environment.NewLine, dependencies = d });

        }

        public void HandleInsertAsTikzStyleClick(SnippetsDataSet.SnippetsTableRow r)
        {
            string toinsert = "", dependencies = "";
            if (!r.IsNull(snippetsTable.NodeStyleColumn) && !(r.NodeStyle.Trim() == ""))
                toinsert += "\\tikzstyle{mynodestyle} = [" + r.NodeStyle + "]" + Environment.NewLine;
            if (!r.IsNull(snippetsTable.EdgeStyleColumn) && !(r.EdgeStyle.Trim() == ""))
                toinsert += "\\tikzstyle{myedgestyle} = [" + r.EdgeStyle + "]" + Environment.NewLine;
            if (!r.IsNull(snippetsTable.DependenciesColumn))
                dependencies = r.Dependencies;

            TheView.RaiseOnInsert(new InsertEventArgs() { code = toinsert, dependencies = dependencies });

        }

        public void HandleUseStyleButtonClick(SnippetsDataSet.SnippetsTableRow r)
        {
            UseStylesEventArgs args = new UseStylesEventArgs();
            if (!r.IsNull(snippetsTable.NodeStyleColumn))
                args.nodestyle = r.NodeStyle;
            if (!r.IsNull(snippetsTable.EdgeStyleColumn))
                args.edgestyle = r.EdgeStyle;
            if (!r.IsNull(snippetsTable.DependenciesColumn))
                args.dependencies = r.Dependencies;
            args.InAddition = System.Windows.Forms.Control.ModifierKeys.HasFlag(Keys.Control);

            TheView.RaiseOnUseStyle(args);
        }
    }

}

