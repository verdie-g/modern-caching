using System.ComponentModel;

/* Required to use records when targeting .NET Standard 2.1 (https://stackoverflow.com/a/62656145/5407910) */
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

[EditorBrowsable(EditorBrowsableState.Never)]
internal class IsExternalInit{}
