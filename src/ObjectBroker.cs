using System;
using System.Collections.Generic;

using System.Text;
using System.Threading;

namespace Portfish
{
    #region Reusing brokers

    internal static class CheckInfoBroker // Reusing
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly CheckInfo[][] _pool = new CheckInfo[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new CheckInfo[0];
            }
        }

        internal static CheckInfo GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                CheckInfo[] temp = new CheckInfo[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new CheckInfo();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(CheckInfo obj)
        {
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class EvalInfoBroker // Reusing
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly EvalInfo[][] _pool = new EvalInfo[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new EvalInfo[0];
            }
        }

        internal static EvalInfo GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                EvalInfo[] temp = new EvalInfo[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new EvalInfo();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(EvalInfo obj)
        {
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class SwapListBroker // Reusing
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly SwapList[][] _pool = new SwapList[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new SwapList[0];
            }
        }

        internal static SwapList GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                SwapList[] temp = new SwapList[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new SwapList();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(SwapList obj)
        {
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class MovesSearchedBroker // Reusing
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly MovesSearched[][] _pool = new MovesSearched[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new MovesSearched[0];
            }
        }

        internal static MovesSearched GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                MovesSearched[] temp = new MovesSearched[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new MovesSearched();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(MovesSearched obj)
        {
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class StateInfoArrayBroker // Reusing
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly StateInfoArray[][] _pool = new StateInfoArray[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new StateInfoArray[0];
            }
        }

        internal static StateInfoArray GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                StateInfoArray[] temp = new StateInfoArray[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new StateInfoArray();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(StateInfoArray obj)
        {
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class PositionBroker // Reusing
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly Position[][] _pool = new Position[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new Position[0];
            }
        }

        internal static Position GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                Position[] temp = new Position[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new Position();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(Position obj)
        {
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    #endregion

    #region Recycling brokers

    internal static class MovePickerBroker // Recycling
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly MovePicker[][] _pool = new MovePicker[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new MovePicker[0];
            }
        }

        internal static MovePicker GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                MovePicker[] temp = new MovePicker[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new MovePicker();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(MovePicker obj)
        {
            obj.Recycle();
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class StateInfoBroker // Recycling
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly StateInfo[][] _pool = new StateInfo[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new StateInfo[0];
            }
        }

        internal static StateInfo GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                StateInfo[] temp = new StateInfo[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new StateInfo();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(StateInfo obj)
        {
            obj.Recycle();
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class LoopStackBroker // Recycling
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly LoopStack[][] _pool = new LoopStack[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new LoopStack[0];
            }
        }

        internal static LoopStack GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                LoopStack[] temp = new LoopStack[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new LoopStack();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(LoopStack obj)
        {
            obj.Recycle();
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    internal static class MListBroker // Recycling
    {
        internal static readonly UInt32[] _cnt = new UInt32[Constants.BROKER_SLOTS];
        internal static readonly MList[][] _pool = new MList[Constants.BROKER_SLOTS][];

        internal static void init()
        {
            for (int i = 0; i < Constants.BROKER_SLOTS; i++)
            {
                _pool[i] = new MList[0];
            }
        }

        internal static MList GetObject()
        {
            int slotID = System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK;
            if (_cnt[slotID] == _pool[slotID].Length)
            {
                int poolLength = _pool[slotID].Length;
                MList[] temp = new MList[poolLength + Constants.BrokerCapacity];
                Array.Copy(_pool[slotID], temp, poolLength);
                for (int i = 0; i < Constants.BrokerCapacity; i++)
                {
                    temp[poolLength + i] = new MList();
                }
                _pool[slotID] = temp;
            }
            return _pool[slotID][_cnt[slotID]++];
        }

        internal static void Free(MList obj)
        {
            obj.Recycle();
            _cnt[System.Threading.Thread.CurrentThread.ManagedThreadId & Constants.BROKER_SLOT_MASK]--;
        }
    }

    #endregion
}
