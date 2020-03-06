using BlueMarsh.Tandoku.Reader;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using LuceneDirectory = Lucene.Net.Store.Directory;

namespace reader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private LuceneDirectory? _dictionaryIndex;
        private IndexSearcher? _dictionarySearcher;

        public MainWindow()
        {
            InitializeComponent();

            (_dictionaryIndex, _dictionarySearcher) = OpenDictionary();

            _image.Source = new BitmapImage(new Uri(
                @"C:\Data\OneDrive\Tandoku Import\Pikake School\Experiment-mupdf\nov1.png"));

            var doc = PdfDocument.Load(
                @"C:\Data\OneDrive\Tandoku Import\Pikake School\Experiment-mupdf\nov.xml");

            var contentWriter = new StringWriter();
            doc.Pages![0].WriteTo(contentWriter);
            _textBox.Text = contentWriter.ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            _dictionarySearcher = null;
            _dictionaryIndex?.Dispose();
            _dictionaryIndex = null;
        }

        private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            string lookupText = Regex.Replace(_textBox.SelectedText, @"[\r\n]", string.Empty);

            var docs = _dictionarySearcher!.Search(new TermQuery(new Term("kanji", lookupText)), 10);
            if (docs.TotalHits == 0)
                docs = _dictionarySearcher!.Search(new TermQuery(new Term("reading", lookupText)), 10);

            var entryWriter = new StringWriter();
            entryWriter.WriteLine(lookupText);

            foreach (var resultDoc in docs.ScoreDocs)
            {
                var doc = _dictionarySearcher.Doc(resultDoc.Doc);
                string kanjiText = string.Join("/",
                    from f in doc.Fields
                    where f.Name == "kanji"
                    select f.GetStringValue());
                string readingText = string.Join("; ",
                    from f in doc.Fields
                    where f.Name == "reading"
                    select f.GetStringValue());
                string glossText = string.Join(Environment.NewLine,
                    from f in doc.Fields
                    where f.Name == "gloss"
                    select f.GetStringValue());

                entryWriter.WriteLine($"{kanjiText} [{readingText}]");
                entryWriter.WriteLine(glossText);
            }

            _dictionary.Text = entryWriter.ToString();
        }

        private (LuceneDirectory, IndexSearcher) OpenDictionary()
        {
            var indexLocation = @"C:\temp\jmdictindex";
            var dir = FSDirectory.Open(indexLocation);

            var reader = DirectoryReader.Open(dir);
            var searcher = new IndexSearcher(reader);

            return (dir, searcher);
        }
    }
}
