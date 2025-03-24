using CommunityToolkit.Mvvm.Messaging;
using MicaStudio.Core.Interfaces.Tabs;
using MicaStudio.Core.Messages.Commands.Files;
using MicaStudio.Core.Messages.Explorer;
using MicaStudio.Panels;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using TextMateSharp.Internal.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using MicaStudio.Utilities;
using WinUIEditor;
using static System.Net.Mime.MediaTypeNames;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;
using MicaStudio.Highlighters;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MicaStudio.Controls
{
	public sealed partial class CodeEditor : UserControl, ITabContent
	{
		public CodeEditor()
		{
			this.InitializeComponent();
		}

		private void addLine(int line, string text)
		{
			ScintillaEditor.Editor.GotoLine(line);
			ScintillaEditor.Editor.AddText(text.Length, text);
			ScintillaEditor.Editor.NewLine();
		}


		public async void OpenFile(StorageFile file)
		{
			if (file is null) return;
			Stopwatch stopwatch = Stopwatch.StartNew();

			/*ScintillaEditor.Editor.Modified -= Editor_Modified;
			ScintillaEditor.Editor.ClearAll();
			ScintillaEditor.Editor.AnnotationClearAll();
						//TEMPORARY::
			keyCount = 190;
			colorToScintillaStyle.Clear();*/
			using (IRandomAccessStream stream = await file.OpenReadAsync())
			{
				using (DataReader reader = new DataReader(stream))
				{
					await reader.LoadAsync((uint)stream.Size);
					// Read raw bytes from file into a byte array
					byte[] fileBytes = new byte[reader.UnconsumedBufferLength];
					reader.ReadBytes(fileBytes);

					// Try to convert bytes to a displayable string
					string fileContent = System.Text.Encoding.Default.GetString(fileBytes);

					// Fallback: If Encoding.Default fails to render properly, display hex values
					if (fileContent.Contains("\0")) // Detect if there's binary data
					{
						fileContent = BitConverter.ToString(fileBytes).Replace("-", " ");
					}

					ScintillaEditor.Editor.SetText(fileContent);
				}
			}
			ScintillaEditor.ResetLexer();
			ScintillaEditor.ApplyDefaultsToDocument();
			//ScintillaEditor.Editor.SetFoldFlags(FoldFlag.LineAfterExpanded); // ENABLE FOLDING



			stopwatch.Stop();
			Debug.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds} ms");


			// TESTING HIGHLIGHTERS
			//BatchHighlighter highlighter = new(ScintillaEditor.Editor, DispatcherQueue);
			//highlighter.FileLoaded(file);
			RandomAccessHighlighter highlighter = new(ScintillaEditor.Editor, DispatcherQueue);
			highlighter.FileLoaded(file);
		}

		/*private void Editor_Modified(Editor sender, ModifiedEventArgs args)
		{
			string line = ScintillaEditor.Editor.GetLine(args.Position);
			ITokenizeLineResult result = grammar.TokenizeLine(line);
			parseTokens(result, line, args.Position, registry);
			int startLine = (int)ScintillaEditor.Editor.LineFromPosition(args.Position); // gets position of first line modified
			int endLine = (int)ScintillaEditor.Editor.LineFromPosition(args.Position + args.Length); // gets position of last line modified
			// gets all lines between range start and end including end line
			int[] linePositionsModified = Enumerable.Range(startLine, endLine - startLine + 1).ToArray();

			// loop through all modified lines and re syntax highlight them
			foreach (int linePosition in linePositionsModified)
			{
				string line = ScintillaEditor.Editor.GetLine(linePosition);
				ITokenizeLineResult result = grammar.TokenizeLine(line);
				parseTokens(result, line, linePosition, registry);
			}
		}*/



		/*
		 * Find { instances, use a stack to track these
		 * If } is encountered then pop from stack and create a folding range from the line of { to the line for }
		 */
		private Stack<long> foldStarts = new(); // keeps track of line positions where { was encountered
		private int level = 3;
		private void foldLine(string line, long linePosition)
		{
			if(line.Contains('{'))
			{
				foldStarts.Push(linePosition);
				level++;
			}
			else if(line.Contains('}'))
			{
				long startPosition = foldStarts.Pop();
				ScintillaEditor.Editor.SetMarginWidthN(2, 16);
				ScintillaEditor.Editor.SetFoldLevel(startPosition, FoldLevel.HeaderFlag);
				for (long i = startPosition + 1; i <= linePosition; i++)
				{
					ScintillaEditor.Editor.SetFoldLevel(i, (FoldLevel)level);
				}

				level--;
			}
		}


		public void clear() => ScintillaEditor.Editor.ClearAll();

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			ScintillaEditor.Editor.AnnotationSetText(0, "test");
			ScintillaEditor.Editor.AnnotationVisible = AnnotationVisible.Indented;

			ScintillaEditor.Editor.IndicSetStyle(8, IndicatorStyle.FullBox);
			ScintillaEditor.Editor.IndicSetFore(8, 0x0000ff);
			ScintillaEditor.Editor.IndicatorCurrent = 8;
			ScintillaEditor.Editor.IndicatorFillRange(0, 7);
		}

		public void Dispose()
		{
			ScintillaEditor.Editor.ClearAll();
			ScintillaEditor.Editor.ClearDocumentStyle();
			ScintillaEditor.Editor.EmptyUndoBuffer();
			ScintillaEditor.Editor.AnnotationClearAll();
		}
	}
}
