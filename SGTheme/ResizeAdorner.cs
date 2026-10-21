using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SGTheme
{
	public class ResizeAdorner: Adorner
	{
		private readonly VisualCollection _visualChildren;
		private readonly Thumb [] _thumbs;
		private UIElement _adornedElement;
		private FrameworkElement _designCanvas;

		// 定义 8 个 Thumb 的位置类型
		private enum ThumbPos
		{
			TopLeft, Top, TopRight,
			Left, Right,
			BottomLeft, Bottom, BottomRight
		}

		public ResizeAdorner (UIElement adornedElement, FrameworkElement designCanvas)
			: base (adornedElement)
		{
			_adornedElement = adornedElement;
			_designCanvas = designCanvas;
			_visualChildren = new VisualCollection (this);

			// 创建 8 个 Thumb，并设置样式和事件
			_thumbs = new Thumb [8];
			for (int i = 0; i < 8; i++)
			{
				var thumb = new Thumb {
					Width = 8,
					Height = 8,
					Cursor = GetCursorForPos ((ThumbPos)i),
					Template = CreateThumbTemplate () // 自定义模板实现蓝心白边
				};
				thumb.DragDelta += Thumb_DragDelta;
				thumb.Tag = (ThumbPos)i;
				_visualChildren.Add (thumb);
				_thumbs [i] = thumb;
			}
		}

		// 为 Thumb 创建自定义样式（蓝色中心，白色边框）
		private ControlTemplate CreateThumbTemplate ()
		{
			var template = new ControlTemplate (typeof (Thumb));
			var border = new FrameworkElementFactory (typeof (Border));
			border.SetValue (Border.BackgroundProperty, Brushes.Blue);
			border.SetValue (Border.BorderBrushProperty, Brushes.White);
			border.SetValue (Border.BorderThicknessProperty, new Thickness (1));
			border.SetValue (Border.CornerRadiusProperty, new CornerRadius (1));
			template.VisualTree = border;
			return template;
		}

		// 根据位置返回对应的光标
		private Cursor GetCursorForPos (ThumbPos pos)
		{
			switch (pos)
			{
				case ThumbPos.TopLeft: return Cursors.SizeNWSE;
				case ThumbPos.TopRight: return Cursors.SizeNESW;
				case ThumbPos.BottomLeft: return Cursors.SizeNESW;
				case ThumbPos.BottomRight: return Cursors.SizeNWSE;
				case ThumbPos.Top: return Cursors.SizeNS;
				case ThumbPos.Bottom: return Cursors.SizeNS;
				case ThumbPos.Left: return Cursors.SizeWE;
				case ThumbPos.Right: return Cursors.SizeWE;
				default: return Cursors.Arrow;
			}
		}

		// 拖拽调整大小或移动
		private void Thumb_DragDelta (object sender, DragDeltaEventArgs e)
		{
			var thumb = sender as Thumb;
			if (thumb == null) return;
			var pos = (ThumbPos)thumb.Tag;
			var element = _adornedElement as FrameworkElement;
			if (element == null) return;

			double deltaH = e.HorizontalChange;
			double deltaV = e.VerticalChange;
			double newLeft = Canvas.GetLeft (element);
			double newTop = Canvas.GetTop (element);
			double newWidth = element.Width;
			double newHeight = element.Height;

			// 如果宽度/高度为 NaN（自动），先取实际值
			if (double.IsNaN (newWidth)) newWidth = element.ActualWidth;
			if (double.IsNaN (newHeight)) newHeight = element.ActualHeight;

			// 最小限制
			double minWidth = 20, minHeight = 20;

			switch (pos)
			{
				case ThumbPos.TopLeft:
					newWidth -= deltaH;
					newHeight -= deltaV;
					if (newWidth >= minWidth) newLeft += deltaH;
					if (newHeight >= minHeight) newTop += deltaV;
					break;
				case ThumbPos.Top:
					newHeight -= deltaV;
					if (newHeight >= minHeight) newTop += deltaV;
					break;
				case ThumbPos.TopRight:
					newWidth += deltaH;
					newHeight -= deltaV;
					if (newHeight >= minHeight) newTop += deltaV;
					break;
				case ThumbPos.Left:
					newWidth -= deltaH;
					if (newWidth >= minWidth) newLeft += deltaH;
					break;
				case ThumbPos.Right:
					newWidth += deltaH;
					break;
				case ThumbPos.BottomLeft:
					newWidth -= deltaH;
					newHeight += deltaV;
					if (newWidth >= minWidth) newLeft += deltaH;
					break;
				case ThumbPos.Bottom:
					newHeight += deltaV;
					break;
				case ThumbPos.BottomRight:
					newWidth += deltaH;
					newHeight += deltaV;
					break;
			}

			// 应用新值
			newWidth = Math.Max (minWidth, newWidth);
			newHeight = Math.Max (minHeight, newHeight);
			element.Width = newWidth;
			element.Height = newHeight;
			Canvas.SetLeft (element, newLeft);
			Canvas.SetTop (element, newTop);

			// 更新 Adorner 布局
			InvalidateVisual ();
		}

		// 布局 Thumb 的位置
		protected override Size ArrangeOverride (Size finalSize)
		{
			double left = 0, top = 0, right = finalSize.Width, bottom = finalSize.Height;
			double half = 4; // Thumb 半宽

			// 八个点的位置（相对于 Adorner 左上角）
			_thumbs [0].Arrange (new Rect (left - half, top - half, 8, 8));    // TopLeft
			_thumbs [1].Arrange (new Rect ((right / 2) - half, top - half, 8, 8)); // Top
			_thumbs [2].Arrange (new Rect (right - half, top - half, 8, 8));    // TopRight
			_thumbs [3].Arrange (new Rect (left - half, (bottom / 2) - half, 8, 8)); // Left
			_thumbs [4].Arrange (new Rect (right - half, (bottom / 2) - half, 8, 8)); // Right
			_thumbs [5].Arrange (new Rect (left - half, bottom - half, 8, 8));   // BottomLeft
			_thumbs [6].Arrange (new Rect ((right / 2) - half, bottom - half, 8, 8)); // Bottom
			_thumbs [7].Arrange (new Rect (right - half, bottom - half, 8, 8));   // BottomRight

			return finalSize;
		}

		// 绘制调整框（虚线边框）
		protected override void OnRender (DrawingContext drawingContext)
		{
			Rect rect = new Rect (0, 0, AdornedElement.RenderSize.Width, AdornedElement.RenderSize.Height);
			drawingContext.DrawRectangle (null, new Pen (Brushes.White, 1) { DashStyle = DashStyles.Dash }, rect);
		}

		protected override int VisualChildrenCount => _visualChildren.Count;
		protected override Visual GetVisualChild (int index) => _visualChildren [index];
	}
}