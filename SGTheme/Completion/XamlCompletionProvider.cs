// Part of SGTheme, licensed under the Microsoft Public License (MS-PL); see LICENSE.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace SGTheme.Completion
{
	public sealed class XamlCompletionResult
	{
		public int StartOffset { get; set; }
		public List<ICompletionData> Items { get; set; }
	}

	public static class XamlCompletionProvider
	{
		private static readonly string[] ElementNames = new [] {
			"Window", "UserControl", "ResourceDictionary", "Style", "Setter", "ControlTemplate",
			"DataTemplate", "ItemsPanelTemplate", "Grid", "StackPanel", "DockPanel", "WrapPanel",
			"Canvas", "Border", "Button", "ToggleButton", "RepeatButton", "Thumb", "TextBlock",
			"TextBox", "PasswordBox", "ComboBox", "ComboBoxItem", "ListBox", "ListView", "TreeView",
			"ScrollViewer", "ScrollBar", "ItemsControl", "ContentControl", "ContentPresenter",
			"Image", "Rectangle", "Ellipse", "Polygon", "Path", "Line", "Viewbox", "Separator",
			"ContextMenu", "MenuItem", "ToolTip", "Popup", "Slider", "ProgressBar", "TabControl",
			"TabItem", "Expander", "GroupBox", "RadioButton", "CheckBox", "Label", "Frame",
			"VisualStateManager", "VisualStateGroup", "VisualState", "Storyboard", "DoubleAnimation",
			"ColorAnimation", "ObjectAnimationUsingKeyFrames", "DiscreteObjectKeyFrame",
			"Trigger", "DataTrigger", "MultiTrigger", "EventTrigger", "BeginStoryboard",
			"Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan", "DockPanel.Dock"
		};

		private static readonly string[] AttributeNames = new [] {
			"x:Key", "x:Name", "x:Class", "x:Uid", "x:Shared", "x:Static", "x:Type", "x:Null",
			"xmlns", "xmlns:x", "xmlns:local", "xmlns:sys", "xmlns:mc", "xmlns:d", "xmlns:snt", "xmlns:p",
			"xmlns:winform", "xmlns:wf", "xmlns:nt", "xmlns:pres",
			"Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
			"Margin", "Padding", "HorizontalAlignment", "VerticalAlignment", "HorizontalContentAlignment",
			"VerticalContentAlignment", "Background", "Foreground", "BorderBrush", "BorderThickness",
			"CornerRadius", "Padding", "Opacity", "Visibility", "IsEnabled", "IsHitTestVisible",
			"Focusable", "Style", "Template", "Content", "ContentTemplate", "Tag", "ToolTip",
			"Text", "FontFamily", "FontSize", "FontWeight", "FontStyle", "TextWrapping",
			"TextTrimming", "TextAlignment", "Source", "Stretch", "StretchDirection", "Orientation",
			"FlowDirection", "DockPanel.Dock", "Grid.Row", "Grid.Column", "Grid.RowSpan",
			"Grid.ColumnSpan", "ItemsSource", "ItemTemplate", "ItemsPanel", "TemplateBinding",
			"MinWidth", "MaxWidth", "MinHeight", "MaxHeight", "Cursor", "AllowDrop",
			"SnapsToDevicePixels", "UseLayoutRounding", "RenderTransformOrigin", "ClipToBounds",
			"TargetType", "BasedOn", "IsDefault", "IsCancel", "IsChecked", "IsThreeState"
		};

		private static readonly string[] MarkupExtensionNames = new [] {
			"DynamicResource", "StaticResource", "Binding", "TemplateBinding", "x:Static", "x:Null", "x:Type"
		};

		private static List<string> _colorNames;
		private static List<string> _namespaceValues;
		private static List<string> _assemblyNames;

		public static XamlCompletionResult GetCompletions (TextEditor editor)
		{
			if (editor == null || editor.Document == null)
				return null;
			int offset = editor.CaretOffset;
			if (offset <= 0)
				return null;
			string text = editor.Document.GetText (0, offset);
			if (IsInsideComment (text))
				return null;

			int tagStart = LastTagStart (text);
			int brace = FindUnclosedBrace (text);
			if (brace >= 0 && tagStart >= 0)
			{
				string inside = text.Substring (brace + 1);
				string extension = FirstToken (inside);
				if (string.IsNullOrEmpty (extension) || !HasWhitespace (inside))
				{
					int start = FindTokenStart (text, offset, IsNameChar);
					return Build (start, text.Substring (start, offset - start), MarkupExtensionNames, "Markup extension", 1);
				}
				if (extension.Equals ("DynamicResource", StringComparison.OrdinalIgnoreCase)
					|| extension.Equals ("StaticResource", StringComparison.OrdinalIgnoreCase))
				{
					int start = FindTokenStart (text, offset, IsResourceKeyChar);
					return Build (start, text.Substring (start, offset - start), GetResourceKeys (), "Resource key", 1);
				}
				return null;
			}

			if (tagStart < 0)
				return null;

			string tag = text.Substring (tagStart);
			bool closingTag = tag.StartsWith ("</", StringComparison.Ordinal);
			bool insideValue = CountQuotes (tag) % 2 == 1;
			if (insideValue)
			{
				int openingQuote = tag.LastIndexOf ('"');
				int equals = openingQuote >= 0 ? tag.LastIndexOf ('=', openingQuote) : tag.LastIndexOf ('=');
				if (equals < 0)
					return null;
				string attribute = AttributeName (tag, equals);
				int start = FindTokenStart (text, offset, IsValueChar);
				string prefix = text.Substring (start, offset - start);
				if (IsXmlnsAttribute (attribute))
				{
					int xmlnsStart = FindTokenStart (text, offset, IsXmlnsValueChar);
					return BuildXmlns (xmlnsStart, text.Substring (xmlnsStart, offset - xmlnsStart));
				}
				if (IsColorAttribute (attribute))
					return Build (start, prefix, GetColorNames (), "Color", 1);
				if (string.Equals (attribute, "Style", StringComparison.OrdinalIgnoreCase))
					return Build (start, prefix, GetResourceKeys (), "Style / resource key", 1);
				return null;
			}

			int nameStart = tagStart + 1;
			if (closingTag)
				nameStart++;
			string beforeCaret = text.Substring (nameStart);
			if (beforeCaret.Trim ().Length == 0 || !HasWhitespace (beforeCaret))
			{
				return Build (nameStart, text.Substring (nameStart), ElementNames, "XAML element", 0);
			}

			int attrStart = FindTokenStart (text, offset, IsNameChar);
			return Build (attrStart, text.Substring (attrStart, offset - attrStart), AttributeNames, "XAML attribute", 0);
		}

		private static XamlCompletionResult Build (int start, string prefix, IEnumerable<string> values, string description, double priority)
		{
			if (start < 0)
				return null;
			List<ICompletionData> items = values
				.Distinct (StringComparer.OrdinalIgnoreCase)
				.Where (v => v.StartsWith (prefix, StringComparison.OrdinalIgnoreCase))
				.OrderBy (v => v, StringComparer.OrdinalIgnoreCase)
				.Select (v => (ICompletionData)new XamlCompletionData (v, description, priority))
				.ToList ();
			if (items.Count == 0)
				return null;
			return new XamlCompletionResult { StartOffset = start, Items = items };
		}

		private static bool IsColorAttribute (string attribute)
		{
			if (string.IsNullOrEmpty (attribute))
				return false;
			string name = attribute;
			int dot = name.LastIndexOf ('.');
			if (dot >= 0)
				name = name.Substring (dot + 1);
			return name.Equals ("Background", StringComparison.OrdinalIgnoreCase)
				|| name.Equals ("Foreground", StringComparison.OrdinalIgnoreCase)
				|| name.Equals ("BorderBrush", StringComparison.OrdinalIgnoreCase)
				|| name.Equals ("Fill", StringComparison.OrdinalIgnoreCase)
				|| name.Equals ("Stroke", StringComparison.OrdinalIgnoreCase)
				|| name.Equals ("Color", StringComparison.OrdinalIgnoreCase)
				|| name.Equals ("Panel.Background", StringComparison.OrdinalIgnoreCase);
		}

		private static string AttributeName (string tag, int equals)
		{
			int i = equals - 1;
			while (i >= 0 && char.IsWhiteSpace (tag [i]))
				i--;
			int end = i + 1;
			while (i >= 0 && IsNameChar (tag [i]))
				i--;
			if (end <= i + 1)
				return string.Empty;
			return tag.Substring (i + 1, end - i - 1);
		}

		private static int FindTokenStart (string text, int offset, Func<char, bool> predicate)
		{
			int i = offset;
			while (i > 0 && predicate (text [i - 1]))
				i--;
			return i;
		}

		private static int LastTagStart (string text)
		{
			int lt = text.LastIndexOf ('<');
			int gt = text.LastIndexOf ('>');
			return lt > gt ? lt : -1;
		}

		private static int FindUnclosedBrace (string text)
		{
			int depth = 0;
			for (int i = text.Length - 1; i >= 0; i--)
			{
				char c = text [i];
				if (c == '}')
					depth++;
				else if (c == '{')
				{
					if (depth == 0)
						return i;
					depth--;
				}
			}
			return -1;
		}

		private static bool IsInsideComment (string text)
		{
			int open = text.LastIndexOf ("<!--", StringComparison.Ordinal);
			if (open < 0)
				return false;
			int close = text.LastIndexOf ("-->", StringComparison.Ordinal);
			return close < open;
		}

		private static bool HasWhitespace (string value)
		{
			for (int i = 0; i < value.Length; i++)
			{
				if (char.IsWhiteSpace (value [i]))
					return true;
			}
			return false;
		}

		private static int CountQuotes (string value)
		{
			int count = 0;
			for (int i = 0; i < value.Length; i++)
			{
				if (value [i] == '"')
					count++;
			}
			return count;
		}

		private static string FirstToken (string value)
		{
			int i = 0;
			while (i < value.Length && char.IsWhiteSpace (value [i]))
				i++;
			int start = i;
			while (i < value.Length && !char.IsWhiteSpace (value [i]) && value [i] != '}' && value [i] != '"')
				i++;
			return value.Substring (start, i - start);
		}

		private static bool IsNameChar (char c)
		{
			return char.IsLetterOrDigit (c) || c == '_' || c == ':' || c == '.' || c == '-';
		}

		private static bool IsResourceKeyChar (char c)
		{
			return char.IsLetterOrDigit (c) || c == '_' || c == '-' || c == '.';
		}

		private static bool IsValueChar (char c)
		{
			return char.IsLetterOrDigit (c) || c == '#' || c == '-' || c == '_' || c == '.';
		}

		private static bool IsXmlnsAttribute (string attribute)
		{
			return !string.IsNullOrEmpty (attribute)
				&& (attribute.Equals ("xmlns", StringComparison.OrdinalIgnoreCase)
					|| attribute.StartsWith ("xmlns:", StringComparison.OrdinalIgnoreCase));
		}

		private static bool IsXmlnsValueChar (char c)
		{
			return char.IsLetterOrDigit (c) || c == '_' || c == '-' || c == '.' || c == ':' || c == ';' || c == '=';
		}

		private static XamlCompletionResult BuildXmlns (int start, string prefix)
		{
			int assemblyIndex = prefix.LastIndexOf ("assembly=", StringComparison.OrdinalIgnoreCase);
			if (assemblyIndex >= 0)
			{
				int assemblyStart = start + assemblyIndex + "assembly=".Length;
				return Build (assemblyStart, prefix.Substring (assemblyIndex + "assembly=".Length),
					GetAssemblyNames (), "Assembly", 1);
			}
			return Build (start, prefix, GetNamespaceValues (), "XAML namespace / assembly", 1);
		}

		private static List<string> GetNamespaceValues ()
		{
			if (_namespaceValues != null)
				return _namespaceValues;

			HashSet<string> values = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			AddKnownNamespace (values, "System", "mscorlib");
			AddKnownNamespace (values, "System.Windows", "PresentationFramework");
			AddKnownNamespace (values, "System.Windows.Controls", "PresentationFramework");
			AddKnownNamespace (values, "System.Windows.Controls.Primitives", "PresentationFramework");
			AddKnownNamespace (values, "System.Windows.Data", "PresentationFramework");
			AddKnownNamespace (values, "System.Windows.Documents", "PresentationFramework");
			AddKnownNamespace (values, "System.Windows.Input", "PresentationCore");
			AddKnownNamespace (values, "System.Windows.Markup", "PresentationFramework");
			AddKnownNamespace (values, "System.Windows.Media", "PresentationCore");
			AddKnownNamespace (values, "System.Windows.Media.Imaging", "PresentationCore");
			AddKnownNamespace (values, "System.Windows.Shapes", "PresentationFramework");
			AddKnownNamespace (values, "System.Windows.Forms", "System.Windows.Forms");
			AddKnownNamespace (values, "System.Drawing", "System.Drawing");
			AddKnownNamespace (values, "SGTheme", "SGTheme");
			AddKnownNamespace (values, "SGTheme.Sidebar", "SGTheme");
			AddKnownNamespace (values, "SGTheme.Tiles", "SGTheme");
			AddKnownNamespace (values, "Sidebar", "CoreLib");
			AddKnownNamespace (values, "Sidebar.Win32", "Native");
			AddKnownNamespace (values, "ICSharpCode.AvalonEdit", "ICSharpCode.AvalonEdit");
			AddKnownNamespace (values, "ICSharpCode.AvalonEdit.Editing", "ICSharpCode.AvalonEdit");
			AddKnownNamespace (values, "ICSharpCode.AvalonEdit.Rendering", "ICSharpCode.AvalonEdit");
			AddKnownNamespace (values, "Newtonsoft.Json", "Newtonsoft.Json");
			AddKnownNamespace (values, "Newtonsoft.Json.Linq", "Newtonsoft.Json");
			AddKnownNamespace (values, "HtmlAgilityPack", "HtmlAgilityPack");

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies ())
			{
				if (assembly.IsDynamic)
					continue;
				string assemblyName;
				try
				{
					assemblyName = assembly.GetName ().Name;
				}
				catch
				{
					continue;
				}
				if (string.IsNullOrEmpty (assemblyName))
					continue;
				Type[] types;
				try
				{
					types = assembly.GetExportedTypes ();
				}
				catch
				{
					continue;
				}
				foreach (Type type in types)
				{
					string ns = type.Namespace;
					if (!string.IsNullOrEmpty (ns) && ns.IndexOf ('+') < 0)
						AddKnownNamespace (values, ns, assemblyName);
				}
			}

			_namespaceValues = values.OrderBy (v => v, StringComparer.OrdinalIgnoreCase).ToList ();
			HashSet<string> assemblyNames = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
			foreach (string value in values)
			{
				int index = value.IndexOf ("assembly=", StringComparison.OrdinalIgnoreCase);
				if (index >= 0)
					assemblyNames.Add (value.Substring (index + "assembly=".Length));
			}
			// Libraries present next to SGTheme are offered for assembly= completion
			// even before they are loaded by the application.
			try
			{
				foreach (string file in Directory.GetFiles (AppDomain.CurrentDomain.BaseDirectory, "*.dll"))
				{
					try
					{
						AssemblyName name = AssemblyName.GetAssemblyName (file);
						if (!string.IsNullOrEmpty (name.Name))
							assemblyNames.Add (name.Name);
					}
					catch { }
				}
			}
			catch { }
			_assemblyNames = assemblyNames.OrderBy (v => v, StringComparer.OrdinalIgnoreCase).ToList ();
			return _namespaceValues;
		}

		private static void AddKnownNamespace (HashSet<string> values, string ns, string assembly)
		{
			if (!string.IsNullOrEmpty (ns) && !string.IsNullOrEmpty (assembly))
				values.Add ("clr-namespace:" + ns + ";assembly=" + assembly);
		}

		private static List<string> GetAssemblyNames ()
		{
			if (_assemblyNames == null)
				GetNamespaceValues ();
			return _assemblyNames;
		}

		private static List<string> GetColorNames ()
		{
			if (_colorNames == null)
			{
				_colorNames = new List<string> ();
				foreach (PropertyInfo property in typeof (Colors).GetProperties (BindingFlags.Public | BindingFlags.Static))
				{
					if (property.PropertyType == typeof (Color))
						_colorNames.Add (property.Name);
				}
			}
			return _colorNames;
		}

		private static List<string> GetResourceKeys ()
		{
			List<string> keys = new List<string> ();
			if (Application.Current != null)
				CollectResourceKeys (Application.Current.Resources, keys, new HashSet<ResourceDictionary> ());
			return keys;
		}

		private static void CollectResourceKeys (ResourceDictionary dictionary, List<string> keys, HashSet<ResourceDictionary> visited)
		{
			if (dictionary == null || visited.Contains (dictionary))
				return;
			visited.Add (dictionary);
			foreach (DictionaryEntry entry in dictionary)
			{
				string key = entry.Key as string;
				if (!string.IsNullOrEmpty (key))
					keys.Add (key);
			}
			foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
				CollectResourceKeys (merged, keys, visited);
		}
	}
}
