// Part of SGTheme, licensed under the Microsoft Public License (MS-PL); see LICENSE.
using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace SGTheme.Completion
{
	public sealed class XamlCompletionData: ICompletionData
	{
		private readonly string _description;
		private readonly double _priority;

		public XamlCompletionData (string text, string description, double priority)
		{
			Text = text;
			_description = description;
			_priority = priority;
		}

		public ImageSource Image
		{
			get { return null; }
		}

		public string Text { get; private set; }

		public object Content
		{
			get { return Text; }
		}

		public object Description
		{
			get { return _description; }
		}

		public double Priority
		{
			get { return _priority; }
		}

		public void Complete (TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
		{
			textArea.Document.Replace (completionSegment, Text);
		}
	}
}
