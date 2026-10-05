using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UContent.Diagnostics
{
    public static class ContentDiagnostics
    {
        private static readonly Dictionary<int, ContentDebugEntry> Entries = new();

        private static int _nextId;

        public static event Action Changed;

        public static bool Enabled { get; set; } = Debug.isDebugBuild;
        public static bool CaptureStackTrace { get; set; }

        public static int Count => Entries.Count;

        internal static int Register(ContentDebugType type, object key, string name = null)
        {
            if (!Enabled)
                return 0;

            int id = ++_nextId;
            string stackTrace = CaptureStackTrace ? Environment.StackTrace : null;

            Entries[id] = new ContentDebugEntry(id, type, name ?? type.ToString(), key?.ToString(), stackTrace);
            NotifyChanged();

            return id;
        }

        internal static void Release(int id)
        {
            if (id == 0)
                return;

            if (!Entries.Remove(id))
                return;

            NotifyChanged();
        }

        public static IReadOnlyList<ContentDebugEntry> GetEntries()
        {
            return Entries.Values.OrderBy(x => x.CreatedAtUtc).ToArray();
        }

        private static void NotifyChanged()
        {
            var changed = Changed;
            if (changed == null)
                return;

            foreach (Action listener in changed.GetInvocationList())
            {
                try
                {
                    listener();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
