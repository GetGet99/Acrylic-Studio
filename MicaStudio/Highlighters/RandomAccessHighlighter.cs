using MicaStudio.Utilities;
using Microsoft.UI.Dispatching;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;
using Windows.Storage;
using WinUIEditor;

namespace MicaStudio.Highlighters
{
	// Highlights Scintilla Editor with random access algorithm
	// Highlights file from top to bottom line by line on file load
	public class RandomAccessHighlighter
	{
		// Maps a textmate colour to a scintilla style key
		private Dictionary<int, int> colorToScintillaStyle = new();
		private IGrammar grammar;
		private Registry registry;
		private Editor Editor;
		private DispatcherQueue DispatcherQueue;
		public RandomAccessHighlighter(Editor editor, DispatcherQueue dispatcherQueue)
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
					{
						await HighlightRange(Editor.FirstVisibleLine, Editor.LinesOnScreen);
						//Editor.UpdateUI += Editor_UpdateUI;
						//Editor.ZoomChanged += Editor_ZoomChanged;
						Editor.SetILexer(0); // Needed to enable STYLENEEDED notifications
						Editor.StyleNeeded += Editor_StyleNeeded;
					}

					stopwatch.Stop();
					Debug.WriteLine($"Execution time: {stopwatch.ElapsedMilliseconds} ms");
				});
			}
			catch { }
		}

		private CancellationTokenSource? cancel; // to cancel previous highlightrange
		private int coount = 0;
		private async void Editor_StyleNeeded(Editor sender, StyleNeededEventArgs args)
		{
			// Cancel old styling operation
			cancel?.Cancel();
			cancel = new CancellationTokenSource();

			// Style from the line that was last styled all the way to the current line needing styling
			var start = Editor.LineFromPosition(Editor.EndStyled);
			var end = Editor.LineFromPosition(args.Position);
			await HighlightRange(start, end, cancel.Token);
			//Debug.WriteLine(coount+ " start: " + start + " end: " + end);
			//coount++;
		}

		// cache to store states per line
		private ConcurrentDictionary<long, IStateStack> cache = new ConcurrentDictionary<long, IStateStack>();
		// Highlight a range of lines from a "start" line
		// We get the cache of the start line then continue highlighting from there
		// We also store rulestacks if they are not cached for a line
		public async Task HighlightRange(long start, long length, CancellationToken? token = null)
		{
			try
			{
				await Task.Run(async () =>
				{
					// Get state of line before start line
					IStateStack? ruleStack = await GetStateOfLine(start - 1, token);
					 // loop through lines highlighting
					for (long i = start; i <= start + length; i++)
					{
						if (token?.IsCancellationRequested ?? false)
							return;

						string line = Editor.GetLine(i);
						ITokenizeLineResult result = grammar.TokenizeLine(line, ruleStack, TimeSpan.MaxValue);
						ruleStack = result.RuleStack;
						cache[i] = ruleStack;

						if (result.Tokens.Count() == 0) continue; // continue if no tokens
						parseTokens(result, line, i);
					}
				});
			}
			catch(Exception e) {
				Debug.WriteLine(e.Message);
			}
		}

		/*
		 * Get the state of a line "linePosition"
		 * This is done by using the cache, if the state is in the cache it is returned
		 * Otherwise we get the closest state right before the line and calculate the state from there to the line
		 */	
		public async Task<IStateStack?> GetStateOfLine(long linePosition, CancellationToken? token)
		{
			IStateStack? ruleStack = null;
			await Task.Run(() =>
			{
				if (cache.ContainsKey(linePosition)) // rulestack cached so return it
					ruleStack = cache[linePosition];
				else
				{ // otherwise calculate rule stacks
					// get the nearest cached line
					var cacheIndex = cache.Keys.LastOrDefault(k => k <= 600);
					for (long i = cacheIndex + 1; i <= linePosition; i++)
					{
						if (token?.IsCancellationRequested ?? false)
							return;

						string line = Editor.GetLine(i);
						ITokenizeLineResult result = grammar.TokenizeLine(line, ruleStack, TimeSpan.MaxValue);
						ruleStack = result.RuleStack;
					//	if(i % 50 == 0 || i < 50)
							cache[i] = ruleStack; // cache it only every 10 lines
					}
				}

			});
			return ruleStack;
		}


		// Colour an individual line with tokens
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

		// broken methods for highlighting
		private async void Editor_ZoomChanged(Editor sender, ZoomChangedEventArgs args)
		{
			cancel?.Cancel();
			cancel = new CancellationTokenSource();

			await HighlightRange(Editor.FirstVisibleLine, Editor.LinesOnScreen, cancel.Token);
		}

		// Update highlighting on scroll
		private async void Editor_UpdateUI(Editor sender, UpdateUIEventArgs args)
		{
			// SC_UPDATE_V_SCROL == 0x04 - scroll updated vertically
			if (args.Updated == 0x04 || args.Updated == 0x08)
			{
				cancel?.Cancel();
				cancel = new CancellationTokenSource();

				await HighlightRange(Editor.FirstVisibleLine, Editor.LinesOnScreen, cancel.Token);
			}
		}
	}
}
