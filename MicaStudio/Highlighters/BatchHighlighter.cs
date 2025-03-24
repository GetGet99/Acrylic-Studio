using MicaStudio.Utilities;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using Windows.Storage;
using WinUIEditor;

namespace MicaStudio.Highlighters
{
	// Highlights Scintilla Editor with batch algorithm
	// Highlights file from top to bottom line by line on file load
	public class BatchHighlighter
	{
		// Maps a textmate colour to a scintilla style key
		private Dictionary<int, int> colorToScintillaStyle = new();
		private IGrammar grammar;
		private Registry registry;
		private Editor Editor;
		private DispatcherQueue DispatcherQueue;
		public BatchHighlighter(Editor editor, DispatcherQueue dispatcherQueue)
		{
			this.Editor = editor;
			DispatcherQueue = dispatcherQueue;
		}

		public async void FileLoaded(StorageFile file)
		{
			try
			{
				await Task.Run(async () =>
				{
					Stopwatch stopwatch = Stopwatch.StartNew();

					RegistryOptions options = new RegistryOptions(ThemeName.DarkPlus);
					registry = new Registry(options);
					grammar = registry.LoadGrammar(options.GetScopeByExtension(file.FileType)); // parameter is initial scope name

					if (grammar is not null)
						await SyntaxHighlightAsync();

					stopwatch.Stop();
					Debug.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds} ms");
				});
			}
			catch { }
		}

		public async Task SyntaxHighlightAsync()
		{
			await Task.Run(() =>
			{ 
				IStateStack? ruleStack = null; // important for state of token, without it multi line comments wont work

				for (int i = 0; i < Editor.LineCount; i++)
				{
					string line = Editor.GetLine(i);
					ITokenizeLineResult result = grammar.TokenizeLine(line, ruleStack, TimeSpan.MaxValue);
					ruleStack = result.RuleStack;

					if (result.Tokens.Count() == 0) break; // return if no tokens
					parseTokens(result, line, i);
				}

				//Editor.Modified += Editor_Modified; // start dynamic highlighting
			});
		}

		private int keyCount = 190;
		private void parseTokens(ITokenizeLineResult result, string line, long linePosition)
		{
			Theme theme = registry.GetTheme();

			foreach (IToken token in result.Tokens)
			{
				int startIndex = (token.StartIndex > line.Length) ? line.Length : token.StartIndex;
				int endIndex = (token.EndIndex > line.Length) ? line.Length : token.EndIndex;
				foreach (var scope in token.Scopes)
				{
					List<ThemeTrieElementRule> themeRules = theme.Match(new string[] { scope });

					foreach (ThemeTrieElementRule themeRule in themeRules)
					{
						DispatcherQueue.TryEnqueue(() =>
						{
							// get position of current line i
							long linePos = Editor.PositionFromLine(linePosition);

							// Register foreground colour to a scintilla style if it does not exist
							if (!colorToScintillaStyle.ContainsKey(themeRule.foreground))
							{
								var color = ColourUtilities.HexToByte(theme.GetColor(themeRule.foreground));
								keyCount++;
								// IMPORTANT: Define a style with a unique KEY mapped to a colour, we will use it to highlight tokens
								Editor.StyleSetFore(keyCount, color);

								// add it to hashmap so we can retrieve it
								colorToScintillaStyle[themeRule.foreground] = keyCount;
							}

							// start styling from token position by using line position and index of token
							Editor.StartStyling(linePos + startIndex, 0);
							// USE the style which we defined earlier for foreground on the token
							// the scintilla style KEYS are in the hashmap
							Editor.SetStyling(endIndex - startIndex, colorToScintillaStyle[themeRule.foreground]);
							/*Debug.WriteLine(
								"      - Matched theme rule: " +
								"[bg: {0}, fg:{1}, fontStyle: {2}]",
								theme.GetColor(themeRule.background),
								theme.GetColor(themeRule.foreground),
								themeRule.fontStyle);*/
						});

					}
				}
			}
		}
	}
}
