using System;
using System.Windows;
using System.Windows.Controls;
namespace WindowsModern.PowerTile
{
	public class PowerTextDecorator: Decorator
	{
		private string [] _texts;
		private Size _lastSize;
		public void SetTexts (string [] texts)
		{
			_texts = texts;
			InvalidateMeasure ();
		}
		protected override Size MeasureOverride (Size constraint)
		{
			if (_texts == null || _texts.Length == 0 || Child == null) return new Size (0, 0);
			var textBlock = Child as TextBlock;
			if (textBlock == null) return base.MeasureOverride (constraint);
			int index = 0;
			bool fits = false;
			do
			{
				string candidate = _texts [index];
				if (!string.IsNullOrEmpty (candidate))
				{
					textBlock.Text = candidate;
					textBlock.Measure (constraint);
					_lastSize = textBlock.DesiredSize;
					fits = _lastSize.Width <= constraint.Width;
				}
				index++;
			}
			while (index < _texts.Length && !fits);
			return _lastSize;
		}
		protected override Size ArrangeOverride (Size arrangeSize)
		{
			if (Child != null) Child.Arrange (new Rect (arrangeSize));
			return arrangeSize;
		}
	}
}