﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Roslyn.VisualStudio.DiagnosticsWindow.OptionsPages
{
    internal class ForceLowMemoryMode
    {
        private int _size = 500;  // default to 500 MB
        private MemoryHogger _hogger;

        public static readonly ForceLowMemoryMode Instance = new ForceLowMemoryMode();

        private ForceLowMemoryMode()
        {
        }

        public int Size
        {
            get { return _size; }
            set { _size = value; }
        }

        public bool Enabled
        {
            get
            {
                return _hogger != null;
            }

            set
            {
                if (value && _hogger == null)
                {
                    _hogger = new MemoryHogger();
                    var tmp = _hogger.PopulateAndMonitorAsync(this.Size);
                }
                else if (!value)
                {
                    var hogger = _hogger;
                    if (hogger != null)
                    {
                        _hogger = null;
                        hogger.Cancel();
                    }
                }
            }
        }

        class MemoryHogger
        {
            private const int BlockSize = 1024 * 1024; // megabyte blocks
            private const int MonitorDelay = 10000; // 10 seconds

            private readonly List<byte[]> _blocks = new List<byte[]>();
            private bool _cancelled;

            public MemoryHogger()
            {
            }

            public int Count
            {
                get { return _blocks.Count; }
            }

            public void Cancel()
            {
                _cancelled = true;
            }

            public Task PopulateAndMonitorAsync(int size)
            {
                // run on background thread
                return Task.Run(() => this.PopulateAndMonitorWorkerAsync(size));
            }

            private async Task PopulateAndMonitorWorkerAsync(int size)
            {
                try
                {
                    for (int n = 0; n < size && !_cancelled; n++)
                    {
                        var block = new byte[BlockSize];

                        // initialize block bits (so they memory actually gets allocated.. silly runtime!)
                        for (int i = 0; i < BlockSize; i++)
                        {
                            block[i] = 0xFF;
                        }

                        _blocks.Add(block);

                        // don't hog the thread
                        await Task.Yield();
                    }
                }
                catch (Exception)
                {
                }

                // monitor memory to keep it paged in
                while (!_cancelled)
                {
                    try
                    {
                        // access all block cells
                        for (var b = 0; b < _blocks.Count && !_cancelled; b++)
                        {
                            var block = _blocks[b];

                            for (int i = 0; i < block.Length; i++)
                            {
                                var tmp = block[i]; // read bytes from block
                            }

                            // don't hog the thread
                            await Task.Yield();
                        }
                    }
                    catch (Exception)
                    {
                    }

                    await Task.Delay(MonitorDelay);
                }

                _blocks.Clear();

                // force garbage collection
                for (int i = 0; i < 5; i++)
                {
                    GC.Collect(GC.MaxGeneration);
                    GC.WaitForPendingFinalizers();
                }
            }
        }
    }
}
