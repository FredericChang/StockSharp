#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: SampleHistoryTesting.SampleHistoryTestingPublic
File: MainWindow.xaml.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace SampleHistoryTesting
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Media;
	using System.Collections.Generic;

	using Ecng.Xaml;
	using Ecng.Common;
	using Ecng.Collections;

	using StockSharp.Algo;
	using StockSharp.Algo.Commissions;
	using StockSharp.Algo.Storages;
	using StockSharp.Algo.Testing;
	using StockSharp.BusinessEntities;
	using StockSharp.Finam;
	using StockSharp.Yahoo;
	using StockSharp.Logging;
	using StockSharp.Messages;
	using StockSharp.Charting;
	using StockSharp.Xaml.Charting;
	using StockSharp.Localization;
	using StockSharp.Configuration;
	using StockSharp.Algo.Candles;

	public partial class MainWindow
	{
		// emulation settings
		private sealed class EmulationInfo
		{
			public bool UseTicks { get; set; }
			public bool UseMarketDepth { get; set; }
			public DataType UseCandle { get; set; }
			public Color CurveColor { get; set; }
			public string StrategyName { get; set; }
			public bool UseOrderLog { get; set; }
			public bool UseLevel1 { get; set; }
            public Level1Fields? BuildField { get; set; }
            public Func<IdGenerator, IMessageAdapter> CustomHistoryAdapter { get; set; }
			public MarketDataStorageCache Cache { get; set; } = new();
		}

		private readonly List<ProgressBar> _progressBars = new();
		private readonly List<CheckBox> _checkBoxes = new();
		private readonly CachedSynchronizedList<HistoryEmulationConnector> _connectors = new();
		private DateTime _startEmulationTime;

		private readonly InMemoryExchangeInfoProvider _exchangeInfoProvider = new();

		public MainWindow()
		{
			InitializeComponent();

			HistoryPath.Folder = Paths.HistoryDataPath;

			SecId.Text = "SBER@TQBR";

			From.EditValue = Paths.HistoryBeginDate;
			To.EditValue = Paths.HistoryEndDate;

			CandleSettings.DataType = DataType.TimeFrame(TimeSpan.FromMinutes(1));

			_progressBars.AddRange(new[]
			{
				TicksProgress,
				TicksAndDepthsProgress,
				DepthsProgress,
				CandlesProgress,
				CandlesAndDepthsProgress,
				OrderLogProgress,
				LastTradeProgress,
				SpreadProgress,
				FinamCandlesProgress,
				YahooCandlesProgress,
				RandomProgress,
			});

			_checkBoxes.AddRange(new[]
			{
				TicksCheckBox,
				TicksAndDepthsCheckBox,
				DepthsCheckBox,
				CandlesCheckBox,
				CandlesAndDepthsCheckBox,
				OrderLogCheckBox,
				LastTradeCheckBox,
				SpreadCheckBox,
				FinamCandlesCheckBox,
				YahooCandlesCheckBox,
				RandomCheckBox,
			});
		}

		private void StartBtnClick(object sender, RoutedEventArgs e)
		{
			if (_connectors.Count > 0)
			{
				foreach (var connector in _connectors.Cache)
					connector.Start();

				return;
			}

			if (HistoryPath.Folder.IsEmpty() || !Directory.Exists(HistoryPath.Folder))
			{
				MessageBox.Show(this, LocalizedStrings.Str3014);
				return;
			}

			if (_connectors.Any(t => t.State != ChannelStates.Stopped))
			{
				MessageBox.Show(this, LocalizedStrings.Str3015);
				return;
			}

			var id = SecId.Text.ToSecurityId();

			//if (secIdParts.Length != 2)
			//{
			//	MessageBox.Show(this, LocalizedStrings.Str3016);
			//	return;
			//}

			var secCode = id.SecurityCode;
			var board = _exchangeInfoProvider.GetOrCreateBoard(id.BoardCode);

			// create test security
			var security = new Security
			{
				Id = SecId.Text, // sec id has the same name as folder with historical data
				Code = secCode,
				Board = board,
				PriceStep = 0.01m,
			};

			// create backtesting modes
			var settings = new[]
			{
				Tuple.Create(
					TicksCheckBox,
					TicksProgress,
					TicksParameterGrid,
					// ticks
					new EmulationInfo
					{
						UseTicks = true,
						CurveColor = Colors.DarkGreen,
						StrategyName = LocalizedStrings.Ticks
					},
					TicksChart,
					TicksEquity,
					TicksPosition),

				Tuple.Create(
					TicksAndDepthsCheckBox,
					TicksAndDepthsProgress,
					TicksAndDepthsParameterGrid,
					// ticks + order book
					new EmulationInfo
					{
						UseTicks = true,
						UseMarketDepth = true,
						CurveColor = Colors.Red,
						StrategyName = LocalizedStrings.XamlStr757
					},
					TicksAndDepthsChart,
					TicksAndDepthsEquity,
					TicksAndDepthsPosition),

				Tuple.Create(
					DepthsCheckBox,
					DepthsProgress,
					DepthsParameterGrid,
					// order book
					new EmulationInfo
					{
						UseMarketDepth = true,
						CurveColor = Colors.OrangeRed,
						StrategyName = LocalizedStrings.MarketDepths
					},
					DepthsChart,
					DepthsEquity,
					DepthsPosition),

				Tuple.Create(
					CandlesCheckBox,
					CandlesProgress,
					CandlesParameterGrid,
					// candles
					new EmulationInfo
					{
						UseCandle = CandleSettings.DataType,
						CurveColor = Colors.DarkBlue,
						StrategyName = LocalizedStrings.Candles
					},
					CandlesChart,
					CandlesEquity,
					CandlesPosition),
				
				Tuple.Create(
					CandlesAndDepthsCheckBox,
					CandlesAndDepthsProgress,
					CandlesAndDepthsParameterGrid,
					// candles + orderbook
					new EmulationInfo
					{
						UseMarketDepth = true,
						UseCandle = CandleSettings.DataType,
						CurveColor = Colors.Cyan,
						StrategyName = LocalizedStrings.XamlStr635
					},
					CandlesAndDepthsChart,
					CandlesAndDepthsEquity,
					CandlesAndDepthsPosition),
			
				Tuple.Create(
					OrderLogCheckBox,
					OrderLogProgress,
					OrderLogParameterGrid,
					// order log
					new EmulationInfo
					{
						UseOrderLog = true,
						CurveColor = Colors.CornflowerBlue,
						StrategyName = LocalizedStrings.OrderLog
					},
					OrderLogChart,
					OrderLogEquity,
					OrderLogPosition),

				Tuple.Create(
					LastTradeCheckBox,
					LastTradeProgress,
					LastTradeParameterGrid,
					// order log
					new EmulationInfo
					{
						UseLevel1 = true,
						CurveColor = Colors.Aquamarine,
						StrategyName = LocalizedStrings.Level1,
						BuildField = Level1Fields.LastTradePrice,
					},
					LastTradeChart,
					LastTradeEquity,
					LastTradePosition),

				Tuple.Create(
					SpreadCheckBox,
					SpreadProgress,
					SpreadParameterGrid,
					// order log
					new EmulationInfo
					{
						UseLevel1 = true,
						CurveColor = Colors.Aquamarine,
						StrategyName = LocalizedStrings.Level1,
						BuildField = Level1Fields.SpreadMiddle,
					},
					SpreadChart,
					SpreadEquity,
					SpreadPosition),

				Tuple.Create(
					FinamCandlesCheckBox,
					FinamCandlesProgress,
					FinamCandlesParameterGrid,
					// candles
					new EmulationInfo
					{
						UseCandle = CandleSettings.DataType,
						CustomHistoryAdapter = g => new FinamMessageAdapter(g),
						CurveColor = Colors.DarkBlue,
						StrategyName = LocalizedStrings.FinamCandles
					},
					FinamCandlesChart,
					FinamCandlesEquity,
					FinamCandlesPosition),

				Tuple.Create(
					YahooCandlesCheckBox,
					YahooCandlesProgress,
					YahooCandlesParameterGrid,
					// candles
					new EmulationInfo
					{
						UseCandle = CandleSettings.DataType,
						CustomHistoryAdapter = g => new YahooMessageAdapter(g),
						CurveColor = Colors.DarkBlue,
						StrategyName = LocalizedStrings.YahooCandles
					},
					YahooCandlesChart,
					YahooCandlesEquity,
					YahooCandlesPosition),

				Tuple.Create(
					RandomCheckBox,
					RandomProgress,
					RandomParameterGrid,
					// candles
					new EmulationInfo
					{
						UseCandle = CandleSettings.DataType,
						CustomHistoryAdapter = g => new OwnMessageAdapter(g),
						CurveColor = Colors.DarkBlue,
						StrategyName = LocalizedStrings.Custom
					},
					RandomChart,
					RandomEquity,
					RandomPosition),
			};

			// storage to historical data
			var storageRegistry = new StorageRegistry
			{
				// set historical path
				DefaultDrive = new LocalMarketDataDrive(HistoryPath.Folder)
			};

			var startTime = ((DateTime)From.EditValue).UtcKind();
			var stopTime = ((DateTime)To.EditValue).UtcKind();

			// (ru only) ОЛ необходимо загружать с 18.45 пред дня, чтобы стаканы строились правильно
			if (OrderLogCheckBox.IsChecked == true)
				startTime = startTime.Subtract(TimeSpan.FromDays(1)).AddHours(18).AddMinutes(45).AddTicks(1).ApplyMoscow().UtcDateTime;

			// set ProgressBar bounds
			_progressBars.ForEach(p =>
			{
				p.Value = 0;
				p.Maximum = 100;
			});
			
			var logManager = new LogManager();
			var fileLogListener = new FileLogListener("sample.log");
			logManager.Listeners.Add(fileLogListener);
			//logManager.Listeners.Add(new DebugLogListener());	// for track logs in output window in Vusial Studio (poor performance).

			var generateDepths = GenDepthsCheckBox.IsChecked == true;
			var maxDepth = MaxDepth.Text.To<int>();
			var maxVolume = MaxVolume.Text.To<int>();
			var secId = security.ToSecurityId();

			SetIsEnabled(false, false, false);

			foreach (var set in settings)
			{
				if (set.Item1.IsChecked == false)
					continue;

				var title = (string)set.Item1.Content;

				ClearChart(set.Item5, set.Item6, set.Item7);

				var progressBar = set.Item2;
				var statistic = set.Item3;
				var emulationInfo = set.Item4;

				var level1Info = new Level1ChangeMessage
				{
					SecurityId = secId,
					ServerTime = startTime,
				}
				.TryAdd(Level1Fields.PriceStep, security.PriceStep)
				.TryAdd(Level1Fields.StepPrice, 0.01m)
				.TryAdd(Level1Fields.MinPrice, 0.01m)
				.TryAdd(Level1Fields.MaxPrice, 1000000m)
				.TryAdd(Level1Fields.MarginBuy, 10000m)
				.TryAdd(Level1Fields.MarginSell, 10000m)
				;

				var secProvider = (ISecurityProvider)new CollectionSecurityProvider(new[] { security });
				var pf = Portfolio.CreateSimulator();
				pf.CurrentValue = 1000;

				// create backtesting connector
				var connector = new HistoryEmulationConnector(secProvider, new[] { pf })
				{
					EmulationAdapter =
					{
						Settings =
						{
							// match order if historical price touched our limit order price. 
							// It is terned off, and price should go through limit order price level
							// (more "severe" test mode)
							MatchOnTouch = false,
						}
					},

					//UseExternalCandleSource = emulationInfo.UseCandleTimeFrame != null,

					//CreateDepthFromOrdersLog = emulationInfo.UseOrderLog,
					//CreateTradesFromOrdersLog = emulationInfo.UseOrderLog,

					HistoryMessageAdapter =
					{
						StorageRegistry = storageRegistry,

						OrderLogMarketDepthBuilders =
						{
							{
								secId,
								new OrderLogMarketDepthBuilder(secId)
							}
						},

						AdapterCache = emulationInfo.Cache,
					},

					// set market time freq as time frame
					//MarketTimeChangedInterval = timeFrame,
				};

				((ILogSource)connector).LogLevel = DebugLogCheckBox.IsChecked == true ? LogLevels.Debug : LogLevels.Info;

				logManager.Sources.Add(connector);

				// create strategy based on 80 5-min и 10 5-min
				var strategy = new SmaStrategy
				{
					LongSma = 80,
					ShortSma = 10,
					Volume = 1,
					Portfolio = connector.Portfolios.First(),
					Security = security,
					Connector = connector,
					LogLevel = DebugLogCheckBox.IsChecked == true ? LogLevels.Debug : LogLevels.Info,

					// by default interval is 1 min,
					// it is excessively for time range with several months
					UnrealizedPnLInterval = ((stopTime - startTime).Ticks / 1000).To<TimeSpan>(),
				};

				if (emulationInfo.UseCandle is not null)
				{
					strategy.CandleType = emulationInfo.UseCandle;

					if (strategy.CandleType != DataType.TimeFrame(TimeSpan.FromMinutes(1)))
					{
						strategy.BuildFrom = DataType.TimeFrame(TimeSpan.FromMinutes(1));
					}
				}
				else if (emulationInfo.UseTicks)
					strategy.BuildFrom = DataType.Ticks;
				else if (emulationInfo.UseLevel1)
				{
					strategy.BuildFrom = DataType.Level1;
					strategy.BuildField = emulationInfo.BuildField;
				}
				else if (emulationInfo.UseOrderLog)
					strategy.BuildFrom = DataType.OrderLog;
				else if (emulationInfo.UseMarketDepth)
					strategy.BuildFrom = DataType.MarketDepth;

				var chart = set.Item5;

				var area = chart.CreateArea();
				chart.AddArea(area);

				var candlesElem = strategy.ChartCandlesElem = chart.CreateCandleElement();
				candlesElem.ShowAxisMarker = false;
				chart.AddElement(area, candlesElem);

				var tradesElem = strategy.ChartTradesElem = chart.CreateTradeElement();
				tradesElem.FullTitle = LocalizedStrings.Trades;
				chart.AddElement(area, tradesElem);

				var shortElem = strategy.ChartShortElem = chart.CreateIndicatorElement();
				shortElem.Color = System.Drawing.Color.Coral;
				shortElem.ShowAxisMarker = false;
				shortElem.FullTitle = strategy.ShortSma.ToString();

				chart.AddElement(area, shortElem);

				var longElem = strategy.ChartLongElem = chart.CreateIndicatorElement();
				longElem.ShowAxisMarker = false;
				longElem.FullTitle = strategy.LongSma.ToString();
				chart.AddElement(area, longElem);

				chart.IsInteracted = false;
				strategy.SetChart(chart);

				logManager.Sources.Add(strategy);

				if (emulationInfo.CustomHistoryAdapter != null)
				{
					connector.Adapter.InnerAdapters.Remove(connector.MarketDataAdapter);

					var emu = connector.EmulationAdapter.Emulator;
					connector.Adapter.InnerAdapters.Add(new EmulationMessageAdapter(emulationInfo.CustomHistoryAdapter(connector.TransactionIdGenerator), new InMemoryMessageChannel(new MessageByLocalTimeQueue(), "History out", err => err.LogError()), true, emu.SecurityProvider, emu.PortfolioProvider, emu.ExchangeInfoProvider));
				}

				// set history range
				connector.HistoryMessageAdapter.StartDate = startTime;
				connector.HistoryMessageAdapter.StopDate = stopTime;

				connector.SecurityReceived += (subscr, s) =>
				{
					if (s != security)
						return;

					// fill level1 values
					connector.EmulationAdapter.SendInMessage(level1Info);

					if (emulationInfo.UseMarketDepth)
					{
						connector.SubscribeMarketDepth(security);

						if	(
								// if order book will be generated
								generateDepths ||
								// or backtesting will be on candles
								emulationInfo.UseCandle is not null
							)
						{
							// if no have order book historical data, but strategy is required,
							// use generator based on last prices
							connector.RegisterMarketDepth(new TrendMarketDepthGenerator(connector.GetSecurityId(security))
							{
								Interval = TimeSpan.FromSeconds(1), // order book freq refresh is 1 sec
								MaxAsksDepth = maxDepth,
								MaxBidsDepth = maxDepth,
								UseTradeVolume = true,
								MaxVolume = maxVolume,
								MinSpreadStepCount = 2,	// min spread generation is 2 pips
								MaxSpreadStepCount = 5,	// max spread generation size (prevent extremely size)
								MaxPriceStepCount = 3	// pips size,
							});
						}
					}

					if (emulationInfo.UseOrderLog)
					{
						connector.SubscribeOrderLog(security);
					}

					if (emulationInfo.UseTicks)
					{
						connector.SubscribeTrades(security);
					}

					if (emulationInfo.UseLevel1)
					{
						connector.SubscribeLevel1(security);
					}
				};

				// fill parameters panel
				statistic.Parameters.Clear();
				statistic.Parameters.AddRange(strategy.StatisticManager.Parameters);

				var equity = set.Item6;

				var pnlCurve = equity.CreateCurve(LocalizedStrings.PnL + " " + emulationInfo.StrategyName, Colors.Green, Colors.Red, ChartIndicatorDrawStyles.Area);
				var unrealizedPnLCurve = equity.CreateCurve(LocalizedStrings.PnLUnreal + " " + emulationInfo.StrategyName, Colors.Black, ChartIndicatorDrawStyles.Line);
				var commissionCurve = equity.CreateCurve(LocalizedStrings.Commission + " " + emulationInfo.StrategyName, Colors.Red, ChartIndicatorDrawStyles.DashedLine);
				
				strategy.PnLReceived2 += (s, t, r, u, c) =>
				{
					var data = equity.CreateData();

					data
						.Group(t)
							.Add(pnlCurve, r - (c ?? 0))
							.Add(unrealizedPnLCurve, u ?? 0)
							.Add(commissionCurve, c ?? 0);

					equity.Draw(data);
				};

				var posItems = set.Item7.CreateCurve(emulationInfo.StrategyName, emulationInfo.CurveColor, ChartIndicatorDrawStyles.Line);

				strategy.PositionReceived += (s, p) =>
				{
					var data = set.Item7.CreateData();

					data
						.Group(p.LocalTime)
							.Add(posItems, p.CurrentValue);

					set.Item7.Draw(data);
				};

				connector.ProgressChanged += steps => this.GuiAsync(() => progressBar.Value = steps);

				connector.StateChanged += () =>
				{
					if (connector.State == ChannelStates.Stopped)
					{
						strategy.Stop();

						SetIsChartEnabled(chart, false);

						if (_connectors.All(c => c.State == ChannelStates.Stopped))
						{
							logManager.Dispose();
							_connectors.Clear();

							SetIsEnabled(true, false, false);
						}

						this.GuiAsync(() =>
						{
							if (connector.IsFinished)
							{
								progressBar.Value = progressBar.Maximum;
								MessageBox.Show(this, LocalizedStrings.Str3024.Put(DateTime.Now - _startEmulationTime), title);
							}
							else
								MessageBox.Show(this, LocalizedStrings.cancelled, title);
						});
					}
					else if (connector.State == ChannelStates.Started)
					{
						if (_connectors.All(c => c.State == ChannelStates.Started))
							SetIsEnabled(false, true, true);

						SetIsChartEnabled(chart, true);
					}
					else if (connector.State == ChannelStates.Suspended)
					{
						if (_connectors.All(c => c.State == ChannelStates.Suspended))
							SetIsEnabled(true, false, true);
					}
				};

				//if (ShowDepth.IsChecked == true)
				//{
				//	MarketDepth.UpdateFormat(security);

				//	connector.NewMessage += message =>
				//	{
				//		if (message is QuoteChangeMessage quoteMsg)
				//			MarketDepth.UpdateDepth(quoteMsg);
				//	};
				//}

				// start strategy before emulation started
				strategy.Start();

				_connectors.Add(connector);

				progressBar.Value = 0;
			}

			_startEmulationTime = DateTime.Now;

			// start emulation
			foreach (var connector in _connectors.Cache)
			{
				// raise NewSecurities and NewPortfolio for full fill strategy properties
				connector.Connect();

				// start historical data loading when connection established successfully and all data subscribed
				connector.Start();

				// 1 cent commission for trade
				connector.SendInMessage(new CommissionRuleMessage
				{
					Rule = new CommissionPerTradeRule { Value = 0.01m }
				});
			}

			TabControl.Items.Cast<TabItem>().First(i => i.Visibility == Visibility.Visible).IsSelected = true;
		}

		private void CheckBoxClick(object sender, RoutedEventArgs e)
		{
			var isEnabled = _checkBoxes.Any(c => c.IsChecked == true);

			StartBtn.IsEnabled = isEnabled;
			TabControl.Visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
		}

		private void StopBtnClick(object sender, RoutedEventArgs e)
		{
			foreach (var connector in _connectors.Cache)
			{
				connector.Disconnect();
			}
		}

		private void PauseBtnClick(object sender, RoutedEventArgs e)
		{
			foreach (var connector in _connectors.Cache)
			{
				connector.Suspend();
			}
		}

		private void ClearChart(IChart chart, EquityCurveChart equity, EquityCurveChart position)
		{
			chart.ClearAreas();
			equity.Clear();
			position.Clear();
		}

		private void SetIsEnabled(bool canStart, bool canSuspend, bool canStop)
		{
			this.GuiAsync(() =>
			{
				StopBtn.IsEnabled = canStop;
				StartBtn.IsEnabled = canStart;
				PauseBtn.IsEnabled = canSuspend;

				foreach (var checkBox in _checkBoxes)
				{
					checkBox.IsEnabled = !canStop;
				}
			});
		}

		private void SetIsChartEnabled(IChart chart, bool started)
		{
			this.GuiAsync(() => chart.IsAutoRange = started);
		}
	}
}