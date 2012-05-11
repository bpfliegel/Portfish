using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;

namespace Portfish
{
    internal delegate void OnChangeUCIOption(UCIOption opt);

    internal sealed class UCIOption
    {
        internal string defaultValue, currentValue, type;
        internal int min = 0, max = 0;
        internal int idx;
        internal string name { get; set; }
        internal OnChangeUCIOption on_change = null;

        internal UCIOption(int index, OnChangeUCIOption fn)
        {
            type = "button";
            idx = index;
            on_change = fn;
        }

        internal UCIOption(int index, string v, OnChangeUCIOption fn)
            : this(index, fn)
        {
            type = "string";
            defaultValue = currentValue = v;
        }

        internal UCIOption(int index, bool v, OnChangeUCIOption fn)
            : this(index, fn)
        {
            type = "check";
            defaultValue = currentValue = (v ? "true" : "false");
        }

        internal UCIOption(int index, int v, int minv, int maxv, OnChangeUCIOption fn)
            : this(index, fn)
        {
            type = "spin"; min = minv; max = maxv;
            defaultValue = currentValue = v.ToString();
        }

        internal string v
        {
            get
            {
                return currentValue;
            }
            set
            {
                Debug.Assert(type.Length > 0);

                if (
                    ((type == "button") || (value != null))
                    &&
                    ((type != "check") || (value == "true" || value == "false"))
                    &&
                    ((type != "spin") || ((int.Parse(value) >= min && int.Parse(value) <= max)))
                    )
                {
                    if (type != "button")
                    {
                        currentValue = value;
                    }

                    if (on_change != null)
                    {
                        on_change(this);
                    }
                }
            }
        }

        internal sealed class UCIOptionIndexComparer : IComparer<UCIOption>
        {
            int IComparer<UCIOption>.Compare(UCIOption x, UCIOption y)
            {
                return x.idx.CompareTo(y.idx);
            }
        }

        internal sealed class UCIOptionNameComparer : IComparer<UCIOption>
        {
            int IComparer<UCIOption>.Compare(UCIOption x, UCIOption y)
            {
                return x.name.CompareTo(y.name);
            }
        }
    }

    /// 'On change' actions, triggered by an option's value change
    internal static class UCIOptionChanges
    {
        internal static void on_eval(UCIOption opt) { Evaluate.init(); }
        internal static void on_threads(UCIOption opt) { Threads.read_uci_options(null); }
        //internal static void on_hash_size(UCIOption opt) { TT.set_size(UInt32.Parse(opt.v)); }
        internal static void on_clear_hash(UCIOption opt) { TT.clear(); }
    }

    internal sealed class OptionMap
    {
        #region Singleton

        private static readonly object _instanceLock = new object();
        private static OptionMap _instance = null;
        internal static OptionMap Instance
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new OptionMap();
                    }
                }
                return _instance;
            }
        }

        #endregion

        private Dictionary<string, UCIOption> o = new Dictionary<string, UCIOption>();

        private int cpu_count()
        {
            int num_cpu;
#if PORTABLE
            num_cpu = Constants.NumberOfCPUs;
#else
            num_cpu=Environment.ProcessorCount;
#endif
            return Math.Min(num_cpu, Constants.MAX_THREADS);
        }

        internal UCIOption this[string name]
        {
            get
            {
                return o[name.ToLowerInvariant()];
            }
        }

        internal bool Contains(string name)
        {
            return o.ContainsKey(name.ToLowerInvariant());
        }

        /// operator<<() is used to output all the UCI options in chronological insertion
        /// order (the idx field) and in the format defined by the UCI protocol.
        public override string ToString()
        {
            List<UCIOption> list = new List<UCIOption>();
            list.AddRange(o.Values);
            list.Sort(new UCIOption.UCIOptionIndexComparer());
            StringBuilder sb = new StringBuilder();
            foreach (UCIOption opt in list)
            {
                sb.Append("\noption name ").Append(opt.name).Append(" type ").Append(opt.type);
                if (opt.type != "button")
                {
                    sb.Append(" default ").Append(opt.defaultValue);
                }
                if (opt.type == "spin")
                {
                    sb.Append(" min ").Append(opt.min).Append(" max ").Append(opt.max);
                }
            }
            return sb.ToString();
        }

        private OptionMap()
        {
            int cpus = Math.Min(cpu_count(), Constants.MAX_THREADS);
            int msd = cpus < 8 ? 4 : 7;

            int idx = 0;
            this.Add("Book File", new UCIOption(idx++, "book.bin", null));
            this.Add("Best Book Move", new UCIOption(idx++, false, null));
            this.Add("Mobility (Middle Game)", new UCIOption(idx++, 100, 0, 200, UCIOptionChanges.on_eval));
            this.Add("Mobility (Endgame)", new UCIOption(idx++, 100, 0, 200, UCIOptionChanges.on_eval));
            this.Add("Passed Pawns (Middle Game)", new UCIOption(idx++, 100, 0, 200, UCIOptionChanges.on_eval));
            this.Add("Passed Pawns (Endgame)", new UCIOption(idx++, 100, 0, 200, UCIOptionChanges.on_eval));
            this.Add("Space", new UCIOption(idx++, 100, 0, 200, UCIOptionChanges.on_eval));
            this.Add("Aggressiveness", new UCIOption(idx++, 100, 0, 200, UCIOptionChanges.on_eval));
            this.Add("Cowardice", new UCIOption(idx++, 100, 0, 200, UCIOptionChanges.on_eval));
            this.Add("Min Split Depth", new UCIOption(idx++, msd, 4, 7, UCIOptionChanges.on_threads));
            this.Add("Max Threads per Split Point", new UCIOption(idx++, 5, 4, 8, UCIOptionChanges.on_threads));
            this.Add("Threads", new UCIOption(idx++, cpus, 1, Constants.MAX_THREADS, UCIOptionChanges.on_threads));
            this.Add("Use Sleeping Threads", new UCIOption(idx++, false, UCIOptionChanges.on_threads)); // changed to false
            this.Add("Hash", new UCIOption(idx++, 32, 4, 8192, null)); // UCIOptionChanges.on_hash_size)); // hack, init on thread
            this.Add("Clear Hash", new UCIOption(idx++, UCIOptionChanges.on_clear_hash));
            this.Add("Ponder", new UCIOption(idx++, true, null));
            this.Add("OwnBook", new UCIOption(idx++, false, null));
            this.Add("MultiPV", new UCIOption(idx++, 1, 1, 500, null));
            this.Add("Skill Level", new UCIOption(idx++, 20, 0, 20, null));
            this.Add("Emergency Move Horizon", new UCIOption(idx++, 40, 0, 50, null));
            this.Add("Emergency Base Time", new UCIOption(idx++, 200, 0, 30000, null));
            this.Add("Emergency Move Time", new UCIOption(idx++, 70, 0, 5000, null));
            this.Add("Minimum Thinking Time", new UCIOption(idx++, 20, 0, 5000, null));
            this.Add("Slow Mover", new UCIOption(idx++, 100, 10, 1000, null));
            this.Add("UCI_Chess960", new UCIOption(idx++, false, null));
            this.Add("UCI_AnalyseMode", new UCIOption(idx++, false, UCIOptionChanges.on_eval));
        }

        private void Add(string optionName, UCIOption option)
        {
            string lname = optionName.ToLowerInvariant();
            option.name = optionName;
            o.Add(lname, option);
        }
    }
}
