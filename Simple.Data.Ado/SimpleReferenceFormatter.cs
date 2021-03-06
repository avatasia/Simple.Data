namespace Simple.Data.Ado
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Schema;

    class SimpleReferenceFormatter
    {
        private readonly IFunctionNameConverter _functionNameConverter = new FunctionNameConverter();
        private readonly DatabaseSchema _schema;
        private readonly ICommandBuilder _commandBuilder;

        public SimpleReferenceFormatter(DatabaseSchema schema, ICommandBuilder commandBuilder)
        {
            _schema = schema;
            _commandBuilder = commandBuilder;
        }

        public string FormatColumnClause(SimpleReference reference)
        {
            return FormatColumnClause(reference, false);
        }

        public string FormatColumnClauseWithoutAlias(SimpleReference reference)
        {
            return FormatColumnClause(reference, true);
        }

        private string FormatColumnClause(SimpleReference reference, bool excludeAlias)
        {
            var formatted = TryFormatAsObjectReference(reference as ObjectReference, excludeAlias)
                            ??
                            TryFormatAsFunctionReference(reference as FunctionReference, excludeAlias)
                            ??
                            TryFormatAsMathReference(reference as MathReference, excludeAlias);

            if (formatted != null) return formatted;

            throw new InvalidOperationException("SimpleReference type not supported.");
        }

        private string FormatObject(object obj)
        {
            var reference = obj as SimpleReference;
            return reference != null ? FormatColumnClause(reference) : obj.ToString();
        }

        private string TryFormatAsMathReference(MathReference mathReference, bool excludeAlias)
        {
            if (ReferenceEquals(mathReference, null)) return null;

            if (excludeAlias || mathReference.GetAlias() == null)
            {
                return string.Format("{0} {1} {2}", FormatObject(mathReference.LeftOperand),
                                     MathOperatorToString(mathReference.Operator),
                                     FormatObject(mathReference.RightOperand));
            }

            return string.Format("{0} {1} {2} AS {3}", FormatObject(mathReference.LeftOperand),
                                 MathOperatorToString(mathReference.Operator),
                                 FormatObject(mathReference.RightOperand),
                                 mathReference.GetAlias());

        }

        private static string MathOperatorToString(MathOperator @operator)
        {
            switch (@operator)
            {
                case MathOperator.Add:
                    return "+";
                case MathOperator.Subtract:
                    return "-";
                case MathOperator.Multiply:
                    return "*";
                case MathOperator.Divide:
                    return "/";
                case MathOperator.Modulo:
                    return "%";
                default:
                    throw new InvalidOperationException("Invalid MathOperator specified.");
            }
        }

        private string TryFormatAsFunctionReference(FunctionReference functionReference, bool excludeAlias)
        {
            if (ReferenceEquals(functionReference, null)) return null;

            var sqlName = _functionNameConverter.ConvertToSqlName(functionReference.Name);
            if (excludeAlias || functionReference.GetAlias() == null)
            {
                return string.Format("{0}({1}{2})", sqlName,
                                     FormatColumnClause(functionReference.Argument),
                                     FormatAdditionalArguments(functionReference.AdditionalArguments));
            }
            return string.Format("{0}({1}{2}) AS {3}", sqlName,
                                       FormatColumnClause(functionReference.Argument),
                                       FormatAdditionalArguments(functionReference.AdditionalArguments),
                                       _schema.QuoteObjectName(functionReference.GetAlias()));
        }

        private string FormatAdditionalArguments(IEnumerable<object> additionalArguments)
        {
            StringBuilder builder = null;
            foreach (var additionalArgument in additionalArguments)
            {
                if (builder == null) builder = new StringBuilder();
                builder.AppendFormat(",{0}", _commandBuilder.AddParameter(additionalArgument));
            }
            return builder != null ? builder.ToString() : string.Empty;
        }

        private string TryFormatAsObjectReference(ObjectReference objectReference, bool excludeAlias)
        {
            if (ReferenceEquals(objectReference, null)) return null;

            var table = _schema.FindTable(objectReference.GetOwner().GetAllObjectNamesDotted());
            var tableName = string.IsNullOrWhiteSpace(objectReference.GetOwner().GetAlias())
                                ? table.QualifiedName
                                : _schema.QuoteObjectName(objectReference.GetOwner().GetAlias());
            var column = table.FindColumn(objectReference.GetName());
            if (excludeAlias || objectReference.GetAlias() == null)
                return string.Format("{0}.{1}", tableName, column.QuotedName);
            else
                return string.Format("{0}.{1} AS {2}", tableName, column.QuotedName,
                                     _schema.QuoteObjectName(objectReference.GetAlias()));
        }

    }
}