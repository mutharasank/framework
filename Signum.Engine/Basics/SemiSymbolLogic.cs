﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Signum.Engine.Maps;
using Signum.Entities;
using Signum.Entities.Basics;
using Signum.Utilities;
using Signum.Utilities.ExpressionTrees;
using Signum.Utilities.Reflection;

namespace Signum.Engine.Extensions.Basics
{
    public static class SemiSymbolLogic<T>
        where T : SemiSymbol
    {
        static ResetLazy<Dictionary<string, T>> lazy;
        static Func<IEnumerable<T>> getSemiSymbols;

        public static void Start(SchemaBuilder sb, Func<IEnumerable<T>> getSemiSymbols)
        {
            if (sb.NotDefined(typeof(SemiSymbolLogic<T>).GetMethod("Start")))
            {
                sb.Include<T>();

                sb.Schema.Initializing[InitLevel.Level0SyncEntities] += () => lazy.Load();
                sb.Schema.Synchronizing += Schema_Synchronizing;
                sb.Schema.Generating += Schema_Generating;

                SemiSymbolLogic<T>.getSemiSymbols = getSemiSymbols;
                lazy = sb.GlobalLazy(() =>
                {
                    var symbols =  CreateSemiSymbols();

                    EnumerableExtensions.JoinStrict(
                         Database.RetrieveAll<T>().Where(a => a.Key.HasText()),
                         symbols,
                         c => c.Key,
                         s => s.Key,
                         (c, s) =>
                         {
                             c.id = s.id;
                             c.Name = s.Name;
                             c.IsNew = false;
                             if (c.Modified != ModifiedState.Sealed)
                                 c.Modified = ModifiedState.Sealed;
                             return c;
                         }
                    , "loading {0}. Consider synchronize".Formato(typeof(T).Name));

                    return symbols.ToDictionary(a => a.Key);

                }, new InvalidateWith(typeof(T)));

                SemiSymbolManager.SemiSymbolDeserialized.Register((T s) =>
                {
                    if (s.Key == null)
                        return;

                    var cached = lazy.Value.GetOrThrow(s.Key);
                    s.id = cached.id;
                    s.Name= cached.Name;
                    s.IsNew = false;
                    s.Modified = ModifiedState.Sealed;
                });
            }
        }

        static SqlPreCommand Schema_Generating()
        {
            Table table = Schema.Current.Table<T>();

            IEnumerable<T> should = CreateSemiSymbols();

            return should.Select(a => table.InsertSqlSync(a)).Combine(Spacing.Simple);
        }

        private static IEnumerable<T> CreateSemiSymbols()
        {
            IEnumerable<T> should = getSemiSymbols().ToList();

            using (Sync.ChangeCulture(Schema.Current.ForceCultureInfo))
                foreach (var item in should)
                    item.Name = item.NiceToString();

            return should;
        }

        static SqlPreCommand Schema_Synchronizing(Replacements replacements)
        {
            Table table = Schema.Current.Table<T>();

            List<T> current = Administrator.TryRetrieveAll<T>(replacements);
            IEnumerable<T> should = CreateSemiSymbols();

            return Synchronizer.SynchronizeScriptReplacing(replacements, typeof(T).Name,
                should.ToDictionary(s => s.Key),
                current.Where(c => c.Key.HasText()).ToDictionary(c => c.Key),
                (k, s) => table.InsertSqlSync(s),
                (k, c) => table.DeleteSqlSync(c),
                (k, s, c) =>
                {
                    var originalName = c.Key;
                    c.Key = s.Key;
                    c.Name = s.Name;
                    return table.UpdateSqlSync(c, comment: originalName);
                }, Spacing.Double);
        }



        static Dictionary<string, T> AssertStarted()
        {
            if (lazy == null)
                throw new InvalidOperationException("{0} has not been started. Someone should have called {0}.Start before".Formato(typeof(SemiSymbolLogic<T>).TypeName()));

            return lazy.Value;
        }

        public static ICollection<T> SemiSymbols
        {
            get { return AssertStarted().Values; }
        }
    }
}
