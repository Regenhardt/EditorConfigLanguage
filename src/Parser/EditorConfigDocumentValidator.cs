﻿using System;
using System.Linq;
using System.Timers;

namespace EditorConfig
{
    partial class EditorConfigDocument
    {
        private const int _validationDelay = 1000;
        private Timer _timer;
        private bool _hasChanged;

        private void InitializeValidator()
        {
            Parsed += DocumentParsed;
        }

        private void DocumentParsed(object sender, EventArgs e)
        {
            _hasChanged = true;

            if (_timer == null)
            {
                _timer = new Timer(1000);
                _timer.Elapsed += TimerElapsed;
            }

            _timer.Enabled = true;
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();

            if (_hasChanged && !IsParsing)
                Validate();

            _hasChanged = false;
        }

        private void Validate()
        {
            foreach (var item in ParseItems)
            {
                switch (item.ItemType)
                {
                    case ItemType.Unknown:
                        ValidateUnknown(item);
                        break;
                }
            }

            ValidateSection();

            foreach (var property in Properties)
            {
                ValidateProperty(property);
            }

            Validated?.Invoke(this, EventArgs.Empty);
        }

        private void ValidateUnknown(ParseItem item)
        {
            item.AddError("Syntax error. Element not valid at current location");
        }
        
        private void ValidateSection()
        {
            foreach (var section in Sections)
            {
                foreach (var property in section.Properties)
                {
                    ValidateProperty(property);

                    if (section.Properties.First(p => p.Keyword.Text.Equals(property.Keyword.Text, StringComparison.OrdinalIgnoreCase)) != property)
                        property.Keyword.AddError(Resources.Text.ValidationDuplicateProperty);
                }

                if (Sections.First(s => s.Item.Text == section.Item.Text) != section)
                    section.Item.AddError(string.Format(Resources.Text.ValidationDuplicateSection, section.Item.Text));
            }
        }

        private void ValidateProperties()
        {
            foreach (var property in Properties)
            {
                if (property != Root)
                    property.Keyword.AddError(Resources.Text.ValidationRootInSection);
            }
        }

        private void ValidateProperty(Property property)
        {
            // Keyword
            if (!SchemaCatalog.TryGetProperty(property.Keyword.Text, out Keyword keyword))
            {
                property.Keyword.AddError(string.Format(Resources.Text.ValidateUnknownKeyword, property.Keyword.Text));
            }

            // Value
            else if (!keyword.Values.Any(v => v.Name.Equals(property.Value.Text, StringComparison.OrdinalIgnoreCase)) &&
                !(int.TryParse(property.Value.Text, out int intValue) && intValue > 0))
            {
                property.Value.AddError(string.Format(Resources.Text.InvalidValue, property.Value.Text, keyword.Name));
            }

            // Severity
            else if (property.Severity == null && property.Value.Text.Equals("true", StringComparison.OrdinalIgnoreCase) && keyword.SupportsSeverity)
            {
                property.Value.AddError(Resources.Text.ValidationMissingSeverity);
            }
            else if (property.Severity != null)
            {
                if (!keyword.SupportsSeverity)
                {
                    property.Severity.AddError(string.Format("The \"{0}\" property does not support a severity suffix", keyword.Name));
                }
                else if (!SchemaCatalog.TryGetSeverity(property.Severity.Text, out Severity severity))
                {
                    property.Severity.AddError(string.Format(Resources.Text.ValidationInvalidSeverity, property.Severity.Text));
                }
            }
        }

        public void DisposeValidator()
        {
            if (_timer != null)
            {
                _timer.Dispose();
            }

            Validated = null;
        }

        public event EventHandler Validated;
    }
}