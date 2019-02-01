namespace Clave.OctoLocal
{
    using System;

    internal class TransformDefinition
    {
        readonly string _definition;

        public TransformDefinition(string definition)
        {
            _definition = definition;
            if (definition.Contains("=>"))
            {
                var separators = new[] { "=>" };
                var parts = definition.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                TransformPatternWithWildcard = parts[0].Trim();
                SourcePatternWithWildcard = parts[1].Trim();
            }
            else
            {
                TransformPatternWithWildcard = definition;
            }
        }

        public string TransformPattern => TransformPatternWithWildcard.Replace("*", string.Empty);
        public string SourcePattern => SourcePatternWithWildcard.Replace("*", string.Empty);
        public string SourcePatternWithWildcard { get; }
        public string TransformPatternWithWildcard { get; }

        public override string ToString()
        {
            return _definition;
        }
    }
}