using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Xunit.ConsoleClient
{
    public interface ILinqTransformer
    {
        void Transform(XElement xml, string outputFileName);
    }

    public class LinqTransformerFactory
    {
        private readonly Dictionary<string, ILinqTransformer> _linqTransformerMap;

        public LinqTransformerFactory()
        {
            _linqTransformerMap = new Dictionary<string, ILinqTransformer>
            {
                { "HTML.xslt", new XUnit2RunnerHtmlResultTransformer() },
                { "NUnitXml.xslt", new XUnit2RunnerNUnitXmlResultTransformer() },
                //{ "xUnit1.xslt", new XUnit2RunnerXmlv1ResultTransformer() }
            }; 
        }

        public ILinqTransformer Create(string key)
        {
            if (_linqTransformerMap.ContainsKey(key))
            {
                return _linqTransformerMap[key];
            }

            return null;
        }
    }

    public class XUnit2RunnerNUnitXmlResultTransformer : ILinqTransformer
    {
        public void Transform(XElement xml, string outputFileName)
        {
            var xmlSerializerHelper = new XmlSerializerHelper();

            var xUnit2RunnerResult = xmlSerializerHelper.Deserialize<XUnit2RunnerResult>(xml.ToString());

            var nUnitRunnerResult = MapToNUnitRunnerResult(xUnit2RunnerResult);

            var nUnitRunnerResultXml = xmlSerializerHelper.Serialize(nUnitRunnerResult);
            //var nUnitRunnerResultXElement = XElement.Parse(nUnitRunnerResultXml);

            //// http://stackoverflow.com/questions/3653132/file-openwrite-appends-instead-of-wiping-contents
            //using (var stream = File.Create(outputFileName))
            //    nUnitRunnerResultXElement.Save(stream);

            File.WriteAllText(outputFileName, nUnitRunnerResultXml, Encoding.UTF8);
        }

        public NUnitRunnerResult MapToNUnitRunnerResult(XUnit2RunnerResult xUnit2RunnerResult)
        {
            var assemblyItems = xUnit2RunnerResult.AssemblyItems;
            var firstAssemblyItem = xUnit2RunnerResult.AssemblyItems.First();

            var success = assemblyItems.Sum(ai => ai.Failed) == 0;
            var result = success ? "Success" : "Failure";

            return new NUnitRunnerResult
            {
                Name = "Test results",

                Date = firstAssemblyItem.RunDate,
                Time = firstAssemblyItem.RunTime,

                Total = assemblyItems.Sum(ai => ai.Total),
                Failures = assemblyItems.Sum(ai => ai.Failed),
                Skipped = assemblyItems.Sum(ai => ai.Skipped),

                Environment = new NUnitRunnerResultEnvironment
                {
                    OSVersion = "unknown",
                    Platform = "unknown",
                    Cwd = "unknown",
                    MachineName = "unknown",
                    User = "unknown",
                    UserDomain = "unknown",
                    NUnitVersion = firstAssemblyItem.TestFramework,
                    ClrVersion = firstAssemblyItem.Environment
                },

                CultureInfo = new NUnitRunnerResultCultureInfo
                {
                    CurrentCulture = "unknown",
                    CurrentUICulture = "unknown"
                },

                TestSuite = new NUnitRunnerResultTestSuite
                {
                    Type = "Assemblies",
                    Name = "xUnit.net Tests",
                    Executed = true,

                    Success = success,
                    ResultText = result,

                    Time = assemblyItems.Sum(ai => ai.Time),

                    TestSuiteOrCaseResults = new TestSuiteOrCaseResults
                    {
                        TestSuiteResults = MapToAssembliesTestSuites(assemblyItems).ToList()
                    }
                }
            };
        }

        private IEnumerable<NUnitRunnerResultTestSuite> MapToAssembliesTestSuites(
            IEnumerable<XUnit2RunnerResultAssembly> xUnit2RunnerResultAssemblies)
        {
            return xUnit2RunnerResultAssemblies.Select(a => MapToAssemblyTestSuite(a));
        }

        private NUnitRunnerResultTestSuite MapToAssemblyTestSuite(XUnit2RunnerResultAssembly xUnit2RunnerResultAssembly)
        {
            var success = xUnit2RunnerResultAssembly.Failed == 0;
            var resultText = success ? "Success" : "Failure";

            return new NUnitRunnerResultTestSuite
            {
                Type = "Assembly",
                Executed = true,

                Name = xUnit2RunnerResultAssembly.Name,

                ResultText = resultText,
                Success = success,

                Time = xUnit2RunnerResultAssembly.Time,

                TestSuiteOrCaseResults = new TestSuiteOrCaseResults
                {
                    TestSuiteResults = MapToCollectionsTestSuites(xUnit2RunnerResultAssembly.Collections).ToList()
                }
            };
        }

        private IEnumerable<NUnitRunnerResultTestSuite> MapToCollectionsTestSuites(
            IEnumerable<XUnit2RunnerResultCollection> xUnit2RunnerResultCollections)
        {
            return xUnit2RunnerResultCollections.Select(c => MapToCollectionTestSuite(c));
        }

        private NUnitRunnerResultTestSuite MapToCollectionTestSuite(XUnit2RunnerResultCollection xUnit2RunnerResultCollection)
        {
            var success = xUnit2RunnerResultCollection.Failed == 0;
            var resultText = success ? "Success" : "Failure";

            return new NUnitRunnerResultTestSuite
            {
                Type = "TestCollection",
                Executed = true,

                Name = xUnit2RunnerResultCollection.Name,

                ResultText = resultText,
                Success = success,

                Time = xUnit2RunnerResultCollection.Time,

                //Failure = xUnit2RunnerResultCollection.Failure == null ? null : new NUnitRunnerResultTestSuiteFailure
                //{
                //    Executed = true,
                //    Name = xUnit2RunnerResultCollection.Failure.
                //},

                // Reason = xUnit2RunnerResultCollection.Reason,

                // TODO - Map Collection.Tests
                //Results = null,
                //TestCaseResults = MapToTestsTestSuites(xUnit2RunnerResultCollection.Tests).ToList()
                TestSuiteOrCaseResults = new TestSuiteOrCaseResults
                {
                    TestCaseResults = MapToTestsTestSuites(xUnit2RunnerResultCollection.Tests).ToList()
                }
            };
        }

        private IEnumerable<NUnitRunnerResultTest> MapToTestsTestSuites(IEnumerable<XUnit2RunnerResultTest> xUnit2RunnerResultTests)
        {
            return xUnit2RunnerResultTests.Select(t => MapToTestCase(t));
        }

        private NUnitRunnerResultTest MapToTestCase(XUnit2RunnerResultTest xUnit2RunnerResultTest)
        {
            string successText = null;
            var resultText = "";
            var executed = false;

            switch (xUnit2RunnerResultTest.Result)
            {
                case "Fail":
                    resultText = "Failure";
                    successText = "False";
                    executed = true;
                    break;
                case "Pass":
                    resultText = "Success";
                    successText = "True";
                    executed = true;
                    break;
                case "Skip":
                    resultText = "Skipped";
                    break;
            }

            return new NUnitRunnerResultTest
            {
                Executed = executed,

                Name = xUnit2RunnerResultTest.Name,

                ResultText = resultText,
                SuccessText = successText,

                Time = xUnit2RunnerResultTest.Time,

                Reason = xUnit2RunnerResultTest.Reason == null ? null : new NUnitRunnerResultTestSuiteReason { Message = xUnit2RunnerResultTest.Reason.Value },

                Properties = xUnit2RunnerResultTest.Traits.Any() ? MapToTestProperties(xUnit2RunnerResultTest.Traits).ToList() : null,

                // TODO - Map Test.failure
                //Failure = xUnit2RunnerResultCollection.Failure == null ? null : new NUnitRunnerResultTestSuiteFailure
                //{
                //    Executed = true,
                //    Name = xUnit2RunnerResultCollection.Failure.
                //},
                Failure = xUnit2RunnerResultTest.Failure
            };
        }

        private IEnumerable<NUnitRunnerResultTestProperty> MapToTestProperties(IEnumerable<XUnit2RunnerResultTrait> xUnit2RunnerResultTraits)
        {
            return xUnit2RunnerResultTraits.Select(t => MapToTestProperty(t));
        }

        private NUnitRunnerResultTestProperty MapToTestProperty(XUnit2RunnerResultTrait xUnit2RunnerResultTrait)
        {
            return new NUnitRunnerResultTestProperty
            {
                Name = xUnit2RunnerResultTrait.Name,
                Value = xUnit2RunnerResultTrait.Value,
            };
        }
    }

    public class XUnit2RunnerXmlv1ResultTransformer : ILinqTransformer
    {
        public void Transform(XElement xml, string outputFileName)
        {
            // TODO - do transform first
            using (var stream = File.OpenWrite(outputFileName))
                xml.Save(stream);
        }
    }

    public class XmlSerializerHelper
    {
        public T Deserialize<T>(string value)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            StringReader textReader = new StringReader(value);
            return (T)xmlSerializer.Deserialize(textReader);
        }

        //public string Serialize<T>(T toSerialize)
        //{
        //    XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
        //    StringWriter textWriter = new StringWriter();
        //    xmlSerializer.Serialize(textWriter, toSerialize);
        //    return textWriter.ToString();
        //}

        public string Serialize<T>(T value)
        {
            var xmlWriterSettings = new XmlWriterSettings()
            {
                OmitXmlDeclaration = true,
                Indent = true
            };

            var xmlSerializerNamespaces = new XmlSerializerNamespaces();
            xmlSerializerNamespaces.Add("", "");

            var stringBuilder = new StringBuilder();
            var xmlWriter = XmlWriter.Create(stringBuilder, xmlWriterSettings);

            var xmlSerializer = new XmlSerializer(typeof(T));
            xmlSerializer.Serialize(xmlWriter, value, xmlSerializerNamespaces);

            xmlWriter.Flush();

            return stringBuilder.ToString();
        }
    }

    // HTML
    public class XUnit2RunnerHtmlResultTransformer : ILinqTransformer
    {
        const char NBSP = '\u00A0';
        const char EM_DASH = '\u2014';

        const char FAILURE_GLYPH = '\u2718';
        const char SUCCESS_GLYPH = '\u2714';
        const char SKIPPED_GLYPH = '\u2762';

        const string doctype = "<!DOCTYPE HTML PUBLIC \" -//W3C//DTD HTML 4.01 Transitional//EN\" \"http://www.w3.org/TR/html4/loose.dtd\">";

        public void Transform(XElement xml, string outputFileName)
        {
            var xUnit2RunnerResult = new XmlSerializerHelper().Deserialize<XUnit2RunnerResult>(xml.ToString());

            var htmlText = GetHtmlDocumentText(xUnit2RunnerResult);

            File.WriteAllText(outputFileName, htmlText);
        }

        private string GetHtmlDocumentText(XUnit2RunnerResult xUnit2RunnerResult)
        {
            XElement bodyXElement = GetBodyXElement(xUnit2RunnerResult);
            return doctype + Environment.NewLine + GetHtmlXElementWithBody(bodyXElement).ToString();
        }

        private XElement GetHtmlXElementWithBody(XElement bodyXElement)
        {
            return new XElement("html",
                GetHtmlHeadXElement(),
                bodyXElement
            );
        }

        private XElement GetHtmlHeadXElement()
        {
            var htmlHeadXml = @"
                    <head>
                        <meta http-equiv='Content-Type' content='text/html; charset=UTF-16' />
                        <title>xUnit.net Test Results</title>
                        <style type='text/css'>
                            body {
                                font-family: Calibri, Verdana, Arial, sans-serif;
                                background-color: White;
                                color: Black;
                            }

                            h2, h3, h4, h5 {
                                margin: 0;
                                padding: 0;
                            }

                            h3 {
                                font-weight: normal;
                            }

                            h4 {
                                margin: 0.5em 0;
                            }

                            h5 {
                                font-weight: normal;
                                font-style: italic;
                                margin-bottom: 0.75em;
                            }

                            h6 {
                                font-size: 0.9em;
                                font-weight: bold;
                                margin: 0.5em 0 0 0.75em;
                                padding: 0;
                            }

                            pre, table {
                                font-family: Consolas;
                                font-size: 0.8em;
                                margin: 0 0 0 1em;
                                padding: 0;
                            }

                            table {
                                padding-bottom: 0.25em;
                            }

                            th {
                                padding: 0 0.5em;
                                border-right: 1px solid #bbb;
                                text-align: left;
                            }

                            td {
                                padding-left: 0.5em;
                            }

                            .divided {
                                border-top: solid 1px #f0f5fa;
                                padding-top: 0.5em;
                            }

                            .row, .altrow {
                                padding: 0.1em 0.3em;
                            }

                            .row {
                                background-color: #f0f5fa;
                            }

                            .altrow {
                                background-color: #e1ebf4;
                            }

                            .success, .failure, .skipped {
                                font-family: Arial Unicode MS;
                                font-weight: normal;
                                float: left;
                                width: 1em;
                                display: block;
                            }

                            .success {
                                color: #0c0;
                            }

                            .failure {
                                color: #c00;
                            }

                            .skipped {
                                color: #cc0;
                            }

                            .timing {
                                float: right;
                            }

                            .indent {
                                margin: 0.25em 0 0.5em 2em;
                            }

                            .clickable {
                                cursor: pointer;
                            }

                            .testcount {
                                font-size: 85%;
                            }
                        </style>
                        <script language='javascript'>
                            function ToggleClass(id) {
                                var elem = document.getElementById(id);
                                if (elem.style.display == 'none') {
                                    elem.style.display = 'block';
                                }
                                else {
                                    elem.style.display = 'none';
                                }
                            }
                        </script>
                    </head>
                ";

            return XElement.Parse(htmlHeadXml);
        }

        public XElement GetBodyXElement(XUnit2RunnerResult xUnit2RunnerResult)
        {
            var assemblyCollections = xUnit2RunnerResult.AssemblyItems.SelectMany(ai => ai.Collections);
            var assemblyCollectionTests = assemblyCollections.SelectMany(fc => fc.Tests);
            var testFailures = assemblyCollectionTests.Where(t => t.Result == "Fail");

            return new XElement("body",
                GetH3XElement("Assemblies Run"),
                GetAssemblyNamesXElements(xUnit2RunnerResult.AssemblyItems.Select(ai => ai.Name)),

                GetH3XElement("Summary"),
                GetSummaryTotalXElement(xUnit2RunnerResult.AssemblyItems.Sum(ai => ai.Total)),
                GetSummaryErrorsXElement(xUnit2RunnerResult.AssemblyItems.Sum(ai => ai.ErrorCount)),
                GetSummaryFailuresXElement(xUnit2RunnerResult.AssemblyItems.Sum(ai => ai.Failed)),
                GetSummarySkippedXElement(xUnit2RunnerResult.AssemblyItems.Sum(ai => ai.Skipped)),

                GetSummaryTimeXElement(xUnit2RunnerResult.AssemblyItems.Sum(ai => ai.Time)),
                GetSummaryFinishedXElement(xUnit2RunnerResult.Timestamp),

                // Errors
                GetErrorXElements(xUnit2RunnerResult.AssemblyItems.SelectMany(ai => ai.Errors)),

                // Test Failures
                testFailures.Any() ?
                    new XElement("div",
                        new XElement("br"),
                        GetH2XElement("Failed tests", "failures")
                    ) : null,
                GetTestFailureXElements(testFailures),

                // All Tests
                new XElement("br"),
                GetH2XElement("All tests", "all"),
                new XElement("h5", "Click test class name to expand/collapse test details"),

                GetTestXElements(assemblyCollectionTests)
            );
        }

        private XElement GetH3XElement(string text)
        {
            return new XElement("h3",
                            new XAttribute("class", "divided"),
                            new XElement("b", text)
                        );
        }

        private XElement GetH2XElement(string text, string id)
        {
            return new XElement("h2",
                    new XElement("a",
                        new XAttribute("id", id),
                        new XText(text)
                    )
                );
        }

        public IEnumerable<XElement> GetAssemblyNamesXElements(IEnumerable<string> assemblyNames)
        {
            return assemblyNames.Select(an =>
                new XElement("div",
                    new XAttribute("class", "assemblyName"),
                    an
                )
            );
        }

        public XElement GetSummaryXElement(IEnumerable<XElement> children)
        {
            return new XElement("div",
                new XAttribute("class", "summary"),
                children
            );
        }

        public XElement GetSummaryTotalXElement(int total)
        {
            return new XElement("span",
                new XAttribute("class", "summaryTotal"),
                new XText("Tests run: "),
                new XElement("a",
                    new XAttribute("href", "#all"),
                    new XElement("b", total)
                ),
                new XText($" { EM_DASH }")
            );
        }

        public XElement GetSummaryCountXElement(int count, string countType)
        {
            if (count == 0)
            {
                return null;
            }

            return new XElement("span",
                new XAttribute("class", $"summary{countType}"),
                new XText($"{countType}: "),
                new XElement("a",
                    new XAttribute("href", $"#{countType.ToLower()}"),
                    new XElement("b", count)
                ),
                new XText(",")
            );
        }

        public XElement GetSummaryErrorsXElement(int errorCount)
        {
            return GetSummaryCountXElement(errorCount, "Errors");
        }

        public XElement GetSummaryFailuresXElement(int failureCount)
        {
            return GetSummaryCountXElement(failureCount, "Failures");
        }

        public XElement GetSummarySkippedXElement(int skippedCount)
        {
            return GetSummaryCountXElement(skippedCount, "Skipped");
        }

        public XElement GetSummaryTimeXElement(decimal time)
        {
            return new XElement("span",
                new XAttribute("class", "summaryTime"),
                new XText("Run time: "),
                new XElement("b",
                    time.ToString("0.0000"),
                    new XText("s")
                ),
                new XText(",")
            );
        }

        public XElement GetSummaryFinishedXElement(string timestamp)
        {
            return new XElement("span",
                new XAttribute("class", "summaryFinished"),
                new XText("Finished: "),
                new XElement("b", timestamp)
            );
        }

        public IEnumerable<XElement> GetTestFailureXElements(IEnumerable<XUnit2RunnerResultTest> tests)
        {
            //< xsl:template match = "test" >

            //    < xsl:if test="child::node()/message">
            //      <pre><xsl:value-of select = "child::node()/message" /></ pre >
            //    </ xsl:if>
            //    <xsl:if test="failure/stack-trace">
            //      <pre><xsl:value-of select = "failure/stack-trace" /></ pre >
            //    </ xsl:if>
            //    <xsl:if test="output">
            //      <h6>Output:</h6>
            //      <pre><xsl:value-of select = "output" /></ pre >
            //    </ xsl:if>
            //    <xsl:if test="traits">
            //      <h6>Traits:</h6>
            //      <table cellspacing = "0" cellpadding="0">
            //        <xsl:apply-templates select = "traits/trait" />
            //      </ table >
            //    </ xsl:if>

            //</xsl:template>

            return tests.Where(t => t != null).Select((test, index) =>
            {
                var timingText = test.Result == "Skip" ? "Skipped" : test.Time.ToString("0.0000") + "s";
                var timingTextXElement = new XElement("span",
                    new XAttribute("class", "timing"),
                    timingText
                );

                var resultGlyphXElementClass = "success";
                var resultGlyphChar = SUCCESS_GLYPH;

                switch (test.Result)
                {
                    case "Skip":
                        resultGlyphXElementClass = "skipped";
                        resultGlyphChar = SKIPPED_GLYPH;
                        break;

                    case "Fail":
                        resultGlyphXElementClass = "failure";
                        resultGlyphChar = FAILURE_GLYPH;
                        break;

                    case "Pass":
                        resultGlyphXElementClass = "success";
                        resultGlyphChar = SUCCESS_GLYPH;
                        break;

                    default:
                        break;
                }

                var resultGlyphCharXElement = new XElement("span",
                    new XAttribute("class", resultGlyphXElementClass),
                    new string(resultGlyphChar, 1)
                );

                var nameXElement = new XText(
                    NBSP.ToString() + test.Name
                );

                return new XElement("div",
                    new XAttribute("class", index % 2 == 0 ? "altrow" : "row"),
                    timingTextXElement,
                    resultGlyphCharXElement,
                    nameXElement,
                    new XElement("br",
                        new XAttribute("class", "all")
                    ),
                    string.IsNullOrWhiteSpace(test.Failure?.Message) ? null : new XElement("pre", test.Failure.Message),
                    string.IsNullOrWhiteSpace(test.Failure?.StackTrace) ? null : new XElement("pre", test.Failure.StackTrace)
                );
            });
        }

        public IEnumerable<XElement> GetErrorXElements(IEnumerable<XUnit2RunnerResultError> errors)
        {
            var typeDescriptionMap = new Dictionary<string, string>
            {
                { "assembly-cleanup", "Test Assembly Cleanup" },
                { "test-collection-cleanup", "Test Assembly Cleanup" },
                { "test-class-cleanup", "Test Class Cleanup" },
                { "test-method-cleanup", "Test Method Cleanup" },
                { "test-case-cleanup", "Test Case Cleanup" },
                { "test-cleanup", "Test Cleanup" },
                { "fatal", "Fatal Error" },
            };

            return errors.Select((error, index) =>
                new XElement("div",
                    new XAttribute("class", index % 2 == 0 ? "error altrow" : "error row"),
                    new XElement("span",
                        new XAttribute("class", "failure"),
                        FAILURE_GLYPH.ToString()
                    ),
                    new XElement("span",
                        typeDescriptionMap[error.Type]
                    ),
                    string.IsNullOrWhiteSpace(error.Name) ? null : new XElement("span", $"({error.Name})"),
                    new XElement("br",
                        new XAttribute("class", "all")
                    ),
                    string.IsNullOrWhiteSpace(error.Failure?.Message) ? null : new XElement("pre", $"({error.Failure.Message})"),
                    string.IsNullOrWhiteSpace(error.StackTrace) ? null : new XElement("pre", $"({error.StackTrace})")
                )
            );
        }

        public IEnumerable<XElement> GetTestXElements(IEnumerable<XUnit2RunnerResultTest> tests)
        {
            var testsByTypeQuery = from test in tests
                                   group test by test.Type into testTypeGroup
                                   select new { Type = testTypeGroup.Key, Tests = testTypeGroup.ToList() }
                              ;

            var testsByTypeGroups = testsByTypeQuery.ToList();

            return testsByTypeGroups.OrderBy(t => t.Type).Select(testsByTypeGroup =>
            {
                var groupTotalTime = testsByTypeGroup.Tests.Sum(t => t.Time);

                var toggleCssClassId = Guid.NewGuid().ToString();
                var toggleCssClass = $"class{ toggleCssClassId }";
                var toggleClassFunctionCall = $"ToggleClass('{ toggleCssClass }')";

                var haveFailures = testsByTypeGroup.Tests.Any(t => t.Result == "Fail");

                return new XElement("div",
                    new XElement("h3",
                        new XElement("span",
                            new XAttribute("class", "timing"),
                            groupTotalTime.ToString("0.0000") + "s"
                        ),
                        new XElement("span",
                            new XAttribute("class", "clickable"),
                            new XAttribute("onclick", toggleClassFunctionCall),
                            new XAttribute("ondblclick", toggleClassFunctionCall)
                        ),
                        haveFailures ? new XElement("span",
                            new XAttribute("class", "failure"),
                            FAILURE_GLYPH
                            ) : null,
                        !haveFailures ? new XElement("span",
                            new XAttribute("class", "success"),
                            SUCCESS_GLYPH
                            ) : null,

                        new XText(NBSP.ToString()),
                        new XText(testsByTypeGroup.Type),

                        new XText(NBSP.ToString()),
                        new XElement("span",
                            new XAttribute("class", "testcount"),
                            new XText("("),
                            new XText(testsByTypeGroup.Tests.Count().ToString()),
                            new XText(NBSP.ToString()),
                            new XText("test"),
                            testsByTypeGroup.Tests.Count() > 1 ? new XText("s") : null,
                            new XText(")")
                       ),
                        new XElement("br",
                            new XAttribute("clear", "all")
                        )
                    ),
                    new XElement("div",
                        new XAttribute("class", "indent"),
                        !haveFailures ? new XAttribute("style", "display: none;") : null,
                        new XAttribute("id", toggleCssClass)
                    ),
                    GetTestFailureXElements(testsByTypeGroup.Tests.OrderBy(t => t.Name))
                );
            });
        }
    }

    [XmlRoot(ElementName = "assemblies")]
    public class XUnit2RunnerResult
    {
        [XmlAttribute("timestamp")]
        public string Timestamp { get; set; }

        [XmlElement("assembly")]
        public List<XUnit2RunnerResultAssembly> AssemblyItems { get; set; } = new List<XUnit2RunnerResultAssembly> { };
    }

    public class XUnit2RunnerResultAssembly
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("config-file")]
        public string ConfigFile { get; set; }

        [XmlAttribute("test-framework")]
        public string TestFramework { get; set; }

        [XmlAttribute("environment")]
        public string Environment { get; set; }

        [XmlAttribute("run-date")]
        public string RunDate { get; set; }

        [XmlAttribute("run-time")]
        public string RunTime { get; set; }

        [XmlAttribute("time")]
        public decimal Time { get; set; }

        [XmlAttribute("total")]
        public int Total { get; set; }

        [XmlAttribute("passed")]
        public int Passed { get; set; }

        [XmlAttribute("failed")]
        public int Failed { get; set; }

        [XmlAttribute("skipped")]
        public int Skipped { get; set; }

        [XmlAttribute("errors")]
        public int ErrorCount { get; set; }

        // [XmlElement("errors")]
        [XmlArray("errors")]
        [XmlArrayItem("error")]
        public List<XUnit2RunnerResultError> Errors { get; set; } = new List<XUnit2RunnerResultError> { };

        [XmlElement("collection")]
        public List<XUnit2RunnerResultCollection> Collections { get; set; } = new List<XUnit2RunnerResultCollection> { };
    }

    public class XUnit2RunnerResultCollection
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("time")]
        public decimal Time { get; set; }

        [XmlAttribute("total")]
        public int Total { get; set; }

        [XmlAttribute("passed")]
        public int Passed { get; set; }

        [XmlAttribute("failed")]
        public int Failed { get; set; }

        [XmlAttribute("skipped")]
        public int Skipped { get; set; }

        // NUnitXml.xslt - Line 92
        // https://xunit.github.io/docs/format-xml-v2.html#collection
        //[XmlIgnore()]
        public XUnit2RunnerResultFailure Failure { get; set; }
        
        // NUnitXml.xslt - Line 95
        // https://xunit.github.io/docs/format-xml-v2.html#collection
        //[XmlIgnore()]
        public string Reason { get; set; }

        [XmlElement("test")]
        public List<XUnit2RunnerResultTest> Tests { get; set; } = new List<XUnit2RunnerResultTest> { };
    }

    public class XUnit2RunnerResultTest
    {
        private XUnit2RunnerResultFailure _failure;

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("method")]
        public string Method { get; set; }

        [XmlAttribute("time")]
        public decimal Time { get; set; }

        [XmlAttribute("result")]
        public string Result { get; set; }

        [XmlArray("traits")]
        [XmlArrayItem("trait")]
        public List<XUnit2RunnerResultTrait> Traits { get; set; } = new List<XUnit2RunnerResultTrait> { };

        //[XmlElement("failure")]
        //public XUnit2RunnerResultFailure Failure { get; set; } = new XUnit2RunnerResultFailure { };

        [XmlElement("reason")]
        public XmlCDataSection Reason { get; set; }

        [XmlElement("output")]
        public string Output { get; set; }

        [XmlElement("failure")]
        public XUnit2RunnerResultFailure Failure
        {
            get { return _failure; }

            set
            {
                _failure = value;

                if (_failure != null)
                {
                    _failure.ParentErrorName = this.Name;
                }
            }
        }
    }

    [XmlType(TypeName = "error")]
    public class XUnit2RunnerResultError
    {
        private XUnit2RunnerResultFailure _failure = new XUnit2RunnerResultFailure();

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("stack-trace")]
        public string StackTrace { get; set; }

        [XmlElement("failure")]
        public XUnit2RunnerResultFailure Failure
        {
            get { return _failure; }

            set
            {
                _failure = value;

                if (_failure != null)
                {
                    _failure.ParentErrorName = this.Name;
                }
            }
        }
    }

    public class XUnit2RunnerResultFailure
    {
        [XmlIgnore]
        public string ParentErrorName { get; set; }

        [XmlAttribute("exception-type")]
        public string ExceptionType { get; set; }

        [XmlElement("message")]
        public string Message { get; set; }

        [XmlElement("stack-trace")]
        public string StackTrace { get; set; }
    }

    public class XUnit2RunnerResultTrait
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }
    }

    // NUnit
    [XmlRoot("test-results")]
    public class NUnitRunnerResult
    {
        // name="Test results" errors="0" inconclusive="0" ignored="0" invalid="0" not-run="0"
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("errors")]
        public int Errors { get; set; }

        [XmlAttribute("inconclusive")]
        public int Inconclusive { get; set; }

        [XmlAttribute("ignored")]
        public int Ignored { get; set; }

        [XmlAttribute("invalid")]
        public int Invalid { get; set; }

        [XmlAttribute("not-run")]
        public int NotRun { get; set; }

        [XmlAttribute("date")]
        public string Date { get; set; }

        [XmlAttribute("time")]
        public string Time { get; set; }

        [XmlAttribute("total")]
        public int Total { get; set; }

        [XmlAttribute("failures")]
        public int Failures { get; set; }

        [XmlAttribute("skipped")]
        public int Skipped { get; set; }

        [XmlElement("environment")]
        public NUnitRunnerResultEnvironment Environment { get; set; } = new NUnitRunnerResultEnvironment();

        [XmlElement("culture-info")]
        public NUnitRunnerResultCultureInfo CultureInfo { get; set; } = new NUnitRunnerResultCultureInfo();

        [XmlElement("test-suite")]
        public NUnitRunnerResultTestSuite TestSuite { get; set; } = new NUnitRunnerResultTestSuite();
    }

    public class NUnitRunnerResultEnvironment
    {
        // os-version="unknown" platform="unknown" cwd="unknown" machine-name="unknown" user="unknown" user-domain="unknown"
        [XmlAttribute("os-version")]
        public string OSVersion { get; set; }

        [XmlAttribute("platform")]
        public string Platform { get; set; }

        [XmlAttribute("cwd")]
        public string Cwd { get; set; }

        [XmlAttribute("machine-name")]
        public string MachineName { get; set; }

        [XmlAttribute("user")]
        public string User { get; set; }

        [XmlAttribute("user-domain")]
        public string UserDomain { get; set; }

        // nunit-version
        // clr-version
        [XmlAttribute("nunit-version")]
        public string NUnitVersion { get; set; }

        [XmlAttribute("clr-version")]
        public string ClrVersion { get; set; }
    }

    public class NUnitRunnerResultCultureInfo
    {
        [XmlAttribute("current-culture")]
        public string CurrentCulture { get; set; }

        [XmlAttribute("current-uiculture")]
        public string CurrentUICulture { get; set; }
    }

    public class NUnitRunnerResultTestSuite
    {
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlIgnore()]
        public bool Executed { get; set; }

        [XmlAttribute("executed")]
        public string ExecutedText
        {
            get { return GetBooleanText(Executed); }
            set { Executed = GetBooleanFromText(value); }
        }

        [XmlAttribute("result")]
        public string ResultText { get; set; }

        [XmlIgnore()]
        public bool Success { get; set; }

        [XmlAttribute("success")]
        public string SuccessText
        {
            get { return GetBooleanText(Success); }
            set { Success = GetBooleanFromText(value); }
        }

        private string GetBooleanText(bool value)
        {
            return value ? "True" : "False";
        }

        private bool GetBooleanFromText(string value)
        {
            return value == "True";
        }

        [XmlAttribute("time")]
        public decimal Time { get; set; }

        [XmlElement("failure")]
        public NUnitRunnerResultTestSuiteFailure Failure { get; set; }

        [XmlElement("reason")]
        public NUnitRunnerResultTestSuiteReason Reason { get; set; }

        [XmlElement("results")]
        public TestSuiteOrCaseResults TestSuiteOrCaseResults { get; set; } = new TestSuiteOrCaseResults();
    }

    [XmlType("results")]
    public class TestSuiteOrCaseResults
    {
        [XmlElement("test-suite")]
        public List<NUnitRunnerResultTestSuite> TestSuiteResults { get; set; }

        [XmlElement("test-case")]
        public List<NUnitRunnerResultTest> TestCaseResults { get; set; }
    }

    public class NUnitRunnerResultTestSuiteFailure
    {
        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("executed")]
        public bool Executed { get; set; }
    }

    public class NUnitRunnerResultTestSuiteReason
    {
        [XmlElement("reason")]
        public string Reason { get; set; }

        [XmlElement("message")]
        public string Message { get; set; }
    }

    [XmlType("test-case")]
    public class NUnitRunnerResultTest
    {
        private XUnit2RunnerResultFailure _failure; // = new XUnit2RunnerResultFailure();

        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlIgnore()]
        public bool Executed { get; set; }

        [XmlAttribute("executed")]
        public string ExecutedText
        {
            get { return GetBooleanText(Executed); }
            set { Executed = GetBooleanFromText(value); }
        }

        private string GetBooleanText(bool value)
        {
            return value ? "True" : "False";
        }

        private bool GetBooleanFromText(string value)
        {
            return value == "True";
        }

        [XmlAttribute("result")]
        public string ResultText { get; set; }

        [XmlAttribute("success")]
        public string SuccessText { get; set; }

        [XmlAttribute("time")]
        public decimal Time { get; set; }

        [XmlElement("failure")]
        public XUnit2RunnerResultFailure Failure
        {
            get { return _failure; }

            set
            {
                _failure = value;

                if (_failure != null)
                {
                    _failure.ParentErrorName = this.Name;
                }
            }
        }

        [XmlElement("reason")]
        public NUnitRunnerResultTestSuiteReason Reason { get; set; }

        [XmlArray("properties")]
        [XmlArrayItem("property")]
        public List<NUnitRunnerResultTestProperty> Properties { get; set; } = new List<NUnitRunnerResultTestProperty>();
    }

    public class NUnitRunnerResultTestProperty
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }
    }

    public class NUnitRunnerResultTestFailure
    {
        [XmlAttribute("exception-type")]
        public string ExceptionType { get; set; }

        [XmlElement("message")]
        public string Message { get; set; }

        [XmlElement("stack-trace")]
        public string StackTrace { get; set; }
    }
}
