using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Windows.Forms;

namespace FDAScripter
{
    public partial class FrmCompileResult : Form
    {
        public FrmCompileResult(ImmutableArray<Diagnostic> diags)
        {
            InitializeComponent();

            BindingList<ErrorItem> errors = new();

            foreach (Diagnostic diag in diags)
                errors.Add(new FrmCompileResult.ErrorItem(diag));

            dgvDiagnostics.AutoGenerateColumns = true;
            dgvDiagnostics.DataSource = errors;
        }

        public class ErrorItem
        {
            public string ID { get; }
            public string Description { get; }
            public string Location { get; }

            public ErrorItem(Diagnostic ErrorItemSource)
            {
                ID = ErrorItemSource.Id;
                Description = ErrorItemSource.GetMessage();
                Location = "Line " + ErrorItemSource.Location.GetLineSpan().StartLinePosition;
            }
        }
    }
}