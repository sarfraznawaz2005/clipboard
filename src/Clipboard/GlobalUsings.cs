// UseWPF and UseWindowsForms together means the SDK's implicit-usings profile picks one
// set and drops the other (and drops System.IO entirely). Explicit global usings here avoid
// that ambiguity instead of aliasing Application/Button/ComboBox/MessageBox in every file.
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Windows;
global using System.Windows.Controls;
