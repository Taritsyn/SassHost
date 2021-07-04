﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
#if NET45 || NET471 || NETSTANDARD
using System.Runtime.InteropServices;
#endif

using JavaScriptEngineSwitcher.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if NET40
using PolyfillsForOldDotNet.System.Runtime.InteropServices;
#endif

using DartSassHost.Helpers;
using DartSassHost.Resources;
using DartSassHost.Utilities;

namespace DartSassHost
{
	/// <summary>
	/// Sass compiler
	/// </summary>
	public sealed class SassCompiler : IDisposable
	{
		/// <summary>
		/// Name of file, which contains a ECMAScript 6+ polyfills
		/// </summary>
		private const string ES6_POLYFILLS_FILE_NAME = "es6-polyfills.min.js";

		/// <summary>
		/// Name of file, which contains a Sass library
		/// </summary>
		private const string SASS_LIBRARY_FILE_NAME = "sass-combined.min.js";

		/// <summary>
		/// Name of file, which contains a Sass helper
		/// </summary>
		private const string SASS_HELPER_FILE_NAME = "sass-helper.min.js";

		/// <summary>
		/// Name of variable, which contains a operating system name
		/// </summary>
		private const string CURRENT_OS_PLATFORM_NAME = "CURRENT_OS_PLATFORM_NAME";

		/// <summary>
		/// Name of variable, which contains a file manager
		/// </summary>
		private const string FILE_MANAGER_VARIABLE_NAME = "FileManager";

		/// <summary>
		/// Default compilation options
		/// </summary>
		private static readonly CompilationOptions _defaultOptions = new CompilationOptions();

		/// <summary>
		/// Compilation options
		/// </summary>
		private CompilationOptions _options;

		/// <summary>
		/// File manager
		/// </summary>
		private IFileManager _fileManager;

		/// <summary>
		/// Delegate that creates an instance of JS engine
		/// </summary>
		private Func<IJsEngine> _createJsEngineInstance;

		/// <summary>
		/// JS engine
		/// </summary>
		private IJsEngine _jsEngine;

		/// <summary>
		/// Synchronizer of Sass compiler initialization
		/// </summary>
		private readonly object _initializationSynchronizer = new object();

		/// <summary>
		/// Flag indicating whether the Sass compiler is initialized
		/// </summary>
		private bool _initialized;

		/// <summary>
		/// Flag indicating whether the Sass compiler is disposed
		/// </summary>
		private InterlockedStatedFlag _disposedFlag = new InterlockedStatedFlag();

		/// <summary>
		/// Version of the Dart Sass library
		/// </summary>
		private string _version = "0.0.0";

		/// <summary>
		/// Gets a version of the Dart Sass library
		/// </summary>
		/// <exception cref="SassCompilerLoadException" />
		/// <exception cref="SassException" />
		public string Version
		{
			get
			{
				Initialize();

				return _version;
			}
		}


		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		public SassCompiler()
			: this(JsEngineSwitcher.Current.CreateDefaultEngine, FileManager.Instance, _defaultOptions)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="createJsEngineInstance">Delegate that creates an instance of JS engine</param>
		public SassCompiler(Func<IJsEngine> createJsEngineInstance)
			: this(createJsEngineInstance, FileManager.Instance, _defaultOptions)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="createJsEngineInstance">Delegate that creates an instance of JS engine</param>
		/// <param name="fileManager">File manager</param>
		public SassCompiler(Func<IJsEngine> createJsEngineInstance, IFileManager fileManager)
			: this(createJsEngineInstance, fileManager, _defaultOptions)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="createJsEngineInstance">Delegate that creates an instance of JS engine</param>
		/// <param name="options">Compilation options</param>
		public SassCompiler(Func<IJsEngine> createJsEngineInstance, CompilationOptions options)
			: this(createJsEngineInstance, FileManager.Instance, options)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="fileManager">File manager</param>
		public SassCompiler(IFileManager fileManager)
			: this(JsEngineSwitcher.Current.CreateDefaultEngine, fileManager, _defaultOptions)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="fileManager">File manager</param>
		/// <param name="options">Processing options</param>
		public SassCompiler(IFileManager fileManager, CompilationOptions options)
			: this(JsEngineSwitcher.Current.CreateDefaultEngine, fileManager, options)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="options">Processing options</param>
		public SassCompiler(CompilationOptions options)
			: this(JsEngineSwitcher.Current.CreateDefaultEngine, FileManager.Instance, options)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="createJsEngineInstance">Delegate that creates an instance of JS engine</param>
		/// <param name="fileManager">File manager</param>
		/// <param name="options">Compilation options</param>
		public SassCompiler(Func<IJsEngine> createJsEngineInstance, IFileManager fileManager, CompilationOptions options)
		{
			if (createJsEngineInstance == null)
			{
				throw new ArgumentNullException(nameof(createJsEngineInstance));
			}

			if (fileManager == null)
			{
				throw new ArgumentNullException(nameof(fileManager));
			}

			_createJsEngineInstance = createJsEngineInstance;
			_fileManager = fileManager;
			_options = options ?? _defaultOptions;
		}

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="jsEngineFactory">JS engine factory</param>
		public SassCompiler(IJsEngineFactory jsEngineFactory)
			: this(jsEngineFactory, _defaultOptions)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="jsEngineFactory">JS engine factory</param>
		/// <param name="fileManager">File manager</param>
		public SassCompiler(IJsEngineFactory jsEngineFactory, IFileManager fileManager)
			: this(jsEngineFactory, fileManager, _defaultOptions)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="jsEngineFactory">JS engine factory</param>
		/// <param name="options">Compilation options</param>
		public SassCompiler(IJsEngineFactory jsEngineFactory, CompilationOptions options)
			: this(jsEngineFactory, FileManager.Instance, options)
		{ }

		/// <summary>
		/// Constructs an instance of the Sass compiler
		/// </summary>
		/// <param name="jsEngineFactory">JS engine factory</param>
		/// <param name="fileManager">File manager</param>
		/// <param name="options">Compilation options</param>
		public SassCompiler(IJsEngineFactory jsEngineFactory, IFileManager fileManager, CompilationOptions options)
		{
			if (jsEngineFactory == null)
			{
				throw new ArgumentNullException(nameof(jsEngineFactory));
			}

			if (fileManager == null)
			{
				throw new ArgumentNullException(nameof(fileManager));
			}

			_createJsEngineInstance = jsEngineFactory.CreateEngine;
			_fileManager = fileManager;
			_options = options ?? _defaultOptions;
		}


		/// <summary>
		/// Initializes a Sass compiler
		/// </summary>
		private void Initialize()
		{
			if (_initialized)
			{
				return;
			}

			lock (_initializationSynchronizer)
			{
				if (_initialized)
				{
					return;
				}

				string serializedOptions = SerializeCompilationOptions(_options);

				try
				{
					_jsEngine = _createJsEngineInstance();
					_jsEngine.EmbedHostObject(FILE_MANAGER_VARIABLE_NAME, _fileManager);
					_jsEngine.SetVariableValue(CURRENT_OS_PLATFORM_NAME, GetCurrentOSPlatformName());

					Assembly assembly = this.GetType()
#if !NET40
						.GetTypeInfo()
#endif
						.Assembly
						;

					_jsEngine.ExecuteResource(ResourceHelpers.GetResourceName(ES6_POLYFILLS_FILE_NAME), assembly);
					_jsEngine.ExecuteResource(ResourceHelpers.GetResourceName(SASS_LIBRARY_FILE_NAME), assembly);
					_jsEngine.ExecuteResource(ResourceHelpers.GetResourceName(SASS_HELPER_FILE_NAME), assembly);
					_jsEngine.Execute($"var sassHelper = new SassHelper({serializedOptions});");

					_version = _jsEngine.Evaluate<string>("SassHelper.getVersion();");
				}
				catch (JsEngineLoadException e)
				{
					throw SassErrorHelpers.WrapCompilerLoadException(e);
				}
				catch (JsException e)
				{
					throw SassErrorHelpers.WrapCompilerLoadException(e, true);
				}

				_initialized = true;
			}
		}

		/// <summary>
		/// Serializes a compilation options to JSON format
		/// </summary>
		/// <param name="options">Compilation options</param>
		/// <returns>Serialized compilation options in JSON format</returns>
		private string SerializeCompilationOptions(CompilationOptions options)
		{
			var optionsJson = new JObject(
				new JProperty("includePaths", new JArray(options.IncludePaths)),
				new JProperty("indentType", GetIndentTypeCode(options.IndentType)),
				new JProperty("indentWidth", options.IndentWidth),
				new JProperty("linefeed", GetLineFeedString(options.LineFeedType)),
				new JProperty("omitSourceMapUrl", options.OmitSourceMapUrl),
				new JProperty("outputStyle", GetOutputStyleCode(options.OutputStyle)),
				new JProperty("sourceMapContents", options.SourceMapIncludeContents),
				new JProperty("sourceMapEmbed", options.InlineSourceMap),
				new JProperty("sourceMapRoot", options.SourceMapRootPath),
				new JProperty("sourceMap", options.SourceMap)
			);

			return optionsJson.ToString();
		}

		/// <summary>
		/// "Compiles" a Sass code to CSS code
		/// </summary>
		/// <param name="content">Text content written on Sass</param>
		/// <param name="indentedSyntax">Flag for whether to enable Sass Indented Syntax
		/// for parsing the data string</param>
		/// <param name="options">Compilation options</param>
		/// <returns>Compilation result</returns>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="SassCompilerLoadException" />
		/// <exception cref="SassCompilationException" />
		/// <exception cref="SassException" />
		public CompilationResult Compile(string content, bool indentedSyntax, CompilationOptions options = null)
		{
			if (content == null)
			{
				throw new ArgumentNullException(
					nameof(content),
					string.Format(Strings.Common_ArgumentIsNull, nameof(content))
				);
			}

			if (string.IsNullOrWhiteSpace(content))
			{
				throw new ArgumentException(
					string.Format(Strings.Common_ArgumentIsEmpty, nameof(content)),
					nameof(content)
				);
			}

			return InnerCompile(content, indentedSyntax, null, null, null, options);
		}

		/// <summary>
		/// "Compiles" a Sass code to CSS code
		/// </summary>
		/// <param name="content">Text content written on Sass</param>
		/// <param name="inputPath">Path to input file</param>
		/// <param name="outputPath">Path to output file</param>
		/// <param name="sourceMapPath">Path to source map file</param>
		/// <param name="options">Compilation options</param>
		/// <returns>Compilation result</returns>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="SassCompilerLoadException" />
		/// <exception cref="SassCompilationException" />
		/// <exception cref="SassException" />
		public CompilationResult Compile(string content, string inputPath, string outputPath = null,
			string sourceMapPath = null, CompilationOptions options = null)
		{
			if (content == null)
			{
				throw new ArgumentNullException(
					nameof(content),
					string.Format(Strings.Common_ArgumentIsNull, nameof(content))
				);
			}

			if (inputPath == null)
			{
				throw new ArgumentNullException(
					nameof(inputPath),
					string.Format(Strings.Common_ArgumentIsNull, nameof(inputPath))
				);
			}

			if (string.IsNullOrWhiteSpace(content))
			{
				throw new ArgumentException(
					string.Format(Strings.Common_ArgumentIsEmpty, nameof(content)),
					nameof(content)
				);
			}

			if (string.IsNullOrWhiteSpace(inputPath))
			{
				throw new ArgumentException(
					string.Format(Strings.Common_ArgumentIsEmpty, nameof(inputPath)),
					nameof(inputPath)
				);
			}

			bool indentedSyntax = GetIndentedSyntax(inputPath);

			return InnerCompile(content, indentedSyntax, inputPath, outputPath, sourceMapPath, options);
		}

		private CompilationResult InnerCompile(string content, bool indentedSyntax, string inputPath,
			string outputPath, string sourceMapPath, CompilationOptions options)
		{
			Initialize();

			string inputFilePath = inputPath;
			string outputFilePath = outputPath;
			string sourceMapFilePath = sourceMapPath;

			ProcessFilePaths(ref inputFilePath, ref outputFilePath, ref sourceMapFilePath);

			string serializedResult = string.Empty;
			string serializedContent = JsonConvert.SerializeObject(content);
			string serializedIndentedSyntax = JsonConvert.SerializeObject(indentedSyntax);
			string serializedInputPath = JsonConvert.SerializeObject(inputFilePath);
			string serializedOutputPath = JsonConvert.SerializeObject(outputFilePath);
			string serializedSourceMapPath = JsonConvert.SerializeObject(sourceMapFilePath);
			string serializedOptions = options != null ? SerializeCompilationOptions(options) : "null";

			try
			{
				serializedResult = _jsEngine.Evaluate<string>("sassHelper.compile(" +
					serializedContent + ", " + serializedIndentedSyntax + ", " +
					serializedInputPath + ", " + serializedOutputPath + ", " +
					serializedSourceMapPath + ", " + serializedOptions +
					");");

			}
			catch (JsException e)
			{
				throw new SassCompilationException(
					string.Format(Strings.Compiler_JsErrorDuringCompilation, e.Message), e)
				{
					Description = e.Message,
					File = inputPath ?? string.Empty
				};
			}

			var resultJson = JObject.Parse(serializedResult);
			var errorsJson = resultJson["errors"] as JArray;

			if (errorsJson != null && errorsJson.Count > 0)
			{
				throw CreateCompilationExceptionFromJson(errorsJson[0]);
			}

			CompilationResult compilationResult = CreateCompilationResultFromJson(resultJson, content, inputPath);

			return compilationResult;
		}

		/// <summary>
		/// "Compiles" a Sass file to CSS code
		/// </summary>
		/// <param name="inputPath">Path to input file</param>
		/// <param name="outputPath">Path to output file</param>
		/// <param name="sourceMapPath">Path to source map file</param>
		/// <param name="options">Compilation options</param>
		/// <returns>Compilation result</returns>
		/// <exception cref="ArgumentException"/>
		/// <exception cref="ArgumentNullException" />
		/// <exception cref="SassCompilerLoadException" />
		/// <exception cref="SassCompilationException" />
		/// <exception cref="SassException" />
		public CompilationResult CompileFile(string inputPath, string outputPath = null,
			string sourceMapPath = null, CompilationOptions options = null)
		{
			if (inputPath == null)
			{
				throw new ArgumentNullException(
					nameof(inputPath),
					string.Format(Strings.Common_ArgumentIsNull, nameof(inputPath))
				);
			}

			if (string.IsNullOrWhiteSpace(inputPath))
			{
				throw new ArgumentException(
					string.Format(Strings.Common_ArgumentIsEmpty, nameof(inputPath)),
					nameof(inputPath)
				);
			}

			Initialize();

			string inputFilePath = inputPath;
			string outputFilePath = outputPath;
			string sourceMapFilePath = sourceMapPath;

			ProcessFilePaths(ref inputFilePath, ref outputFilePath, ref sourceMapFilePath);

			IFileManager fileManager = _fileManager;
			if (fileManager != null && !fileManager.FileExists(inputPath))
			{
				string description = string.Format("No such file or directory: {0}", inputPath);
				string message = string.Format("Error: {0}", description);

				throw new SassCompilationException(message)
				{
					Description = description,
					Status = 3,
					File = string.Empty,
					LineNumber = 0,
					ColumnNumber = 0,
					SourceFragment = string.Empty
				};
			}

			string serializedResult = string.Empty;
			string serializedInputPath = JsonConvert.SerializeObject(inputFilePath);
			string serializedOutputPath = JsonConvert.SerializeObject(outputFilePath);
			string serializedSourceMapPath = JsonConvert.SerializeObject(sourceMapFilePath);
			string serializedOptions = options != null ? SerializeCompilationOptions(options) : "null";

			try
			{
				serializedResult = _jsEngine.Evaluate<string>("sassHelper.compileFile(" +
					serializedInputPath + ", " + serializedOutputPath + ", " +
					serializedSourceMapPath + ", " + serializedOptions +
					");");

			}
			catch (JsException e)
			{
				throw new SassCompilationException(
					string.Format(Strings.Compiler_JsErrorDuringCompilation, e.Message), e)
				{
					Description = e.Message,
					File = inputPath ?? string.Empty
				};
			}

			var resultJson = JObject.Parse(serializedResult);
			var errorsJson = resultJson["errors"] as JArray;

			if (errorsJson != null && errorsJson.Count > 0)
			{
				throw CreateCompilationExceptionFromJson(errorsJson[0]);
			}

			CompilationResult compilationResult = CreateCompilationResultFromJson(resultJson, string.Empty, inputPath);

			return compilationResult;
		}

		private static string GetCurrentOSPlatformName()
		{
			string platformName;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				platformName = "win32";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				platformName = "linux";
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				platformName = "darwin";
			}
			else
			{
				platformName = "unknown";
			}

			return platformName;
		}

		[MethodImpl((MethodImplOptions)256 /* AggressiveInlining */)]
		private static void ProcessFilePaths(ref string inputPath, ref string outputPath, ref string sourceMapPath)
		{
			inputPath = !string.IsNullOrWhiteSpace(inputPath) ? inputPath : string.Empty;
			outputPath = !string.IsNullOrWhiteSpace(outputPath) ? outputPath : string.Empty;
			sourceMapPath = !string.IsNullOrWhiteSpace(sourceMapPath) ? sourceMapPath : string.Empty;

			if (inputPath.Length > 0 || outputPath.Length > 0)
			{
				outputPath = outputPath.Length > 0 ?
					outputPath : Path.ChangeExtension(inputPath, ".css");
				sourceMapPath = sourceMapPath.Length > 0 ?
					sourceMapPath : Path.ChangeExtension(outputPath, ".css.map");
			}
		}

		private SassCompilationException CreateCompilationExceptionFromJson(JToken error)
		{
			var description = error.Value<string>("description");
			var status = error.Value<int>("status");
			var type = error.Value<string>("type");
			var file = error.Value<string>("file");
			string documentName = Path.GetFileName(file);
			var lineNumber = error.Value<int>("lineNumber");
			var columnNumber = error.Value<int>("columnNumber");
			var content = error.Value<string>("source");
			string sourceFragment = SourceCodeNavigator.GetSourceFragment(content,
				new SourceCodeNodeCoordinates(lineNumber, columnNumber));
			string sourceLineFragment = TextHelpers.GetTextFragment(content, lineNumber, columnNumber);
			string message = SassErrorHelpers.GenerateCompilationErrorMessage(type, description, documentName,
				lineNumber, columnNumber, sourceLineFragment);

			var compilationException = new SassCompilationException(message)
			{
				Description = description,
				Status = status,
				File = file,
				LineNumber = lineNumber,
				ColumnNumber = columnNumber,
				SourceFragment = sourceFragment
			};

			return compilationException;
		}

		private CompilationResult CreateCompilationResultFromJson(JObject resultJson, string content,
			string inputPath)
		{
			string compiledContent = resultJson.Value<string>("compiledContent");
			string sourceMap = resultJson.Value<string>("sourceMap");
			IList<string> includedFilePaths = resultJson.Value<JArray>("includedFilePaths")
				.ToObject<IList<string>>()
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList()
				;
			var compilationResult = new CompilationResult(compiledContent, sourceMap, includedFilePaths);

			return compilationResult;
		}

		private static bool GetIndentedSyntax(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return false;
			}

			string fileExtension = Path.GetExtension(path);
			bool indentedSyntax = string.Equals(fileExtension, ".SASS", StringComparison.OrdinalIgnoreCase);

			return indentedSyntax;
		}

		private static string GetIndentTypeCode(IndentType type)
		{
			string typeCode = type == IndentType.Tab ? "tab" : "space";

			return typeCode;
		}

		private static string GetLineFeedString(LineFeedType type)
		{
			string lineFeed;

			switch (type)
			{
				case LineFeedType.Cr:
					lineFeed = "cr";
					break;
				case LineFeedType.CrLf:
					lineFeed = "crlf";
					break;
				case LineFeedType.Lf:
					lineFeed = "lf";
					break;
				case LineFeedType.LfCr:
					lineFeed = "lfcr";
					break;
				default:
					throw new NotSupportedException();
			}

			return lineFeed;
		}

		private static string GetOutputStyleCode(OutputStyle style)
		{
			string styleCode = style == OutputStyle.Expanded ? "expanded" : "compressed";

			return styleCode;
		}

		/// <summary>
		/// Destroys object
		/// </summary>
		public void Dispose()
		{
			if (_disposedFlag.Set())
			{
				if (_jsEngine != null)
				{
					_jsEngine.RemoveVariable(FILE_MANAGER_VARIABLE_NAME);

					_jsEngine.Dispose();
					_jsEngine = null;
				}

				_options = null;
				_fileManager = null;
				_createJsEngineInstance = null;
			}
		}
	}
}