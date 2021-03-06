using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Signum.Entities;
using Signum.Services;
using Signum.Windows.Operations;
using System.Windows.Media;
using Signum.Utilities;
using System.Reflection;
using Win = System.Windows;
using System.Linq.Expressions;
using Signum.Utilities.Reflection;
using System.Windows;
using System.Windows.Controls;
using Signum.Entities.Reflection;
using Signum.Utilities.ExpressionTrees;
using System.Windows.Automation;
using Signum.Entities.Basics;
using System.Collections.Concurrent;

namespace Signum.Windows.Operations
{
    public static class OperationClient
    {
        public static OperationManager Manager { get; private set; }

        public static void Start(OperationManager operationManager)
        {
            if (Navigator.Manager.NotDefined(MethodInfo.GetCurrentMethod()))
            {
                Manager = operationManager;

                Navigator.AddSetting(new EntitySettings<OperationLogDN>() { View = e => new OperationLog() });

                Navigator.Manager.GetButtonBarElementGlobal += Manager.ButtonBar_GetButtonBarElement;

                Constructor.Manager.GeneralConstructor += Manager.ConstructorManager_GeneralConstructor;

                SearchControl.GetContextMenuItems += Manager.SearchControl_GetConstructorFromManyMenuItems;
                SearchControl.GetContextMenuItems += Manager.SearchControl_GetEntityOperationMenuItem;

                LinksClient.RegisterEntityLinks<IdentifiableEntity>((entity, control) => new[]
                { 
                    entity.GetType() == typeof(OperationLogDN) ? null : 
                        new QuickLinkExplore(new ExploreOptions(typeof(OperationLogDN), "Target", entity)
                        {OrderOptions ={ new OrderOption("Start") }}){ IsShy = true}
                });
            }
        }

        public static bool SaveProtected(Type type)
        {
            return Manager.SaveProtected(type);
        }

        public static readonly DependencyProperty ConstructFromOperationKeyProperty =
            DependencyProperty.RegisterAttached("ConstructFromOperationKey", typeof(OperationSymbol), typeof(OperationClient), new UIPropertyMetadata(null));
        public static OperationSymbol GetConstructFromOperationKey(DependencyObject obj)
        {
            return (OperationSymbol)obj.GetValue(ConstructFromOperationKeyProperty);
        }

        public static void SetConstructFromOperationKey(DependencyObject obj, OperationSymbol value)
        {
            obj.SetValue(ConstructFromOperationKeyProperty, value);
        }

        public static ImageSource GetImage(OperationSymbol operation)
        {
            return Manager.GetImage(operation, Manager.Settings.TryGetC(operation));
        }

        public static string GetText(OperationSymbol operation)
        {
            return Manager.GetText(operation, Manager.Settings.TryGetC(operation));
        }

        public static void AddSetting(OperationSettings setting)
        {
            Manager.Settings.AddOrThrow(setting.OperationSymbol, setting, "EntitySettings {0} repeated");
        }

        public static void AddSettings(List<OperationSettings> settings)
        {
            Manager.Settings.AddRange(settings, s => s.OperationSymbol, s => s, "EntitySettings");
        }
    }

    public class OperationManager
    {
        public Dictionary<OperationSymbol, OperationSettings> Settings = new Dictionary<OperationSymbol, OperationSettings>();

        public Func<OperationSymbol, bool> IsSave = e => e.ToString().StartsWith("Save");

        public List<OperationColor> BackgroundColors = new List<OperationColor>
        {
            new OperationColor(a => a.OperationType == OperationType.Execute && a.Lite == false) { Color = Colors.Blue}, 
            new OperationColor(a => a.OperationType == OperationType.Execute && a.Lite == true) { Color = Colors.Yellow}, 
            new OperationColor(e => e.OperationType == OperationType.Delete ) { Color = Colors.Red }, 
        };

        public T GetSettings<T>(OperationSymbol operation)
            where T : OperationSettings
        {
            OperationSettings settings = Settings.TryGetC(operation);
            if (settings != null)
            {
                var result = settings as T;

                if (result == null)
                    throw new InvalidOperationException("{0}({1}) should be a {2}".Formato(settings.GetType().TypeName(), operation.Key, typeof(T).TypeName()));

                return result;
            }

            return null;
        }

        ConcurrentDictionary<Type, List<OperationInfo>> operationInfoCache = new ConcurrentDictionary<Type, List<OperationInfo>>();
        public List<OperationInfo> OperationInfos(Type entityType)
        {
            return operationInfoCache.GetOrAdd(entityType, t => Server.Return((IOperationServer o) => o.GetOperationInfos(t)));
        }

        protected internal virtual List<FrameworkElement> ButtonBar_GetButtonBarElement(object entity, ButtonBarEventArgs ctx)
        {
            IdentifiableEntity ident = entity as IdentifiableEntity;

            if (ident == null)
                return null;

            Type type = ident.GetType();

            var operations = (from oi in OperationInfos(ident.GetType())
                              where oi.IsEntityOperation && (oi.AllowsNew.Value || !ident.IsNew)
                              let os = GetSettings<EntityOperationSettings>(oi.OperationSymbol)
                              let eoc = new EntityOperationContext
                              {
                                  Entity = (IdentifiableEntity)entity,
                                  EntityControl = ctx.MainControl,
                                  OperationInfo = oi,
                                  ViewButtons = ctx.ViewButtons,
                                  ShowOperations = ctx.ShowOperations,
                                  OperationSettings = os,
                              }
                              where (os != null && os.IsVisible != null) ? os.IsVisible(eoc) : ctx.ShowOperations
                              select eoc).ToList();

            if (operations.Any(eoc => eoc.OperationInfo.HasCanExecute == true))
            {
                Dictionary<OperationSymbol, string> canExecutes = Server.Return((IOperationServer os) => os.GetCanExecuteAll(ident));
                foreach (var eoc in operations)
                {
                    var ce = canExecutes.TryGetC(eoc.OperationInfo.OperationSymbol);
                    if (ce != null && ce.HasText())
                        eoc.CanExecute = ce;
                }
            }

            List<FrameworkElement> buttons = new List<FrameworkElement>();
            Dictionary<EntityOperationGroup, ToolBarButton> groups = new Dictionary<EntityOperationGroup,ToolBarButton>();
            Dictionary<EntityOperationGroup, List<FrameworkElement>> groupButtons = new Dictionary<EntityOperationGroup,List<FrameworkElement>>();
          
            foreach (var eoc in operations)
            {
                if (eoc.OperationInfo.OperationType == OperationType.ConstructorFrom &&
                   (eoc.OperationSettings == null || !eoc.OperationSettings.AvoidMoveToSearchControl))
                {
                    if(EntityOperationToolBarButton.MoveToSearchControls(eoc))
                        continue; 
                }

                EntityOperationGroup group = GetDefaultGroup(eoc);

                if(group != null)
                {
                    var list = groupButtons.GetOrCreate(group, () =>
                    {
                        var tbb = EntityOperationToolBarButton.CreateGroupContainer(group);
                        groups.Add(group, tbb);
                        buttons.Add(tbb);
                        return new List<FrameworkElement>();
                    });

                   list.Add(EntityOperationToolBarButton.NewMenuItem(eoc, group));
                }
                else
                {
                    buttons.Add(EntityOperationToolBarButton.NewToolbarButton(eoc));
                }
            }

            foreach (var gr in groups)
            {
                var cm = gr.Value.ContextMenu;
                foreach (var b in groupButtons.GetOrThrow(gr.Key).OrderBy(Common.GetOrder))
                    cm.Items.Add(b);
            }

            return buttons.ToList();
        }

        private EntityOperationGroup GetDefaultGroup(EntityOperationContext eoc)
        {
            if (eoc.OperationSettings != null && eoc.OperationSettings.Group != null)
                return eoc.OperationSettings.Group == EntityOperationGroup.None ? null : eoc.OperationSettings.Group;

            if (eoc.OperationInfo.OperationType == OperationType.ConstructorFrom)
                return EntityOperationGroup.Create;

            return null;
        }

        protected internal virtual Brush GetBackground(OperationInfo oi, OperationSettings os)
        {
            if (os != null && os.Color != null)
                return new SolidColorBrush(os.Color.Value);

            var bc = BackgroundColors.LastOrDefault(a => a.IsApplicable(oi));
            if (bc != null)
                return new SolidColorBrush(bc.Color);

            return null;
        }

        protected internal virtual ImageSource GetImage(OperationSymbol operation, OperationSettings os)
        {
            if (os != null && os.Icon != null)
                return os.Icon;

            if (IsSave(operation))
                return ImageLoader.GetImageSortName("save.png");

            return null;
        }

        protected internal virtual string GetText(OperationSymbol operation, OperationSettings os)
        {
            if (os != null && os.Text != null)
                return os.Text;

            return operation.NiceToString();
        }



        protected internal virtual object ConstructorManager_GeneralConstructor(Type entityType, FrameworkElement element)
        {
            if (!entityType.IsIIdentifiable())
                return null;

            var dic = (from oi in OperationInfos(entityType)
                       where oi.OperationType == OperationType.Constructor
                       let os = GetSettings<ConstructorSettings>(oi.OperationSymbol)
                       where os == null || os.IsVisible == null || os.IsVisible(oi)
                       select new { OperationInfo = oi, OperationSettings = os }).ToDictionary(a => a.OperationInfo.OperationSymbol);

            if (dic.Count == 0)
                return null;

            var win = Window.GetWindow(element);

            OperationSymbol selected = null;
            if (dic.Count == 1)
            {
                selected = dic.Keys.SingleEx();
            }
            else
            {
                if (!SelectorWindow.ShowDialog(dic.Keys.ToArray(), out selected,
                    elementIcon: k => OperationClient.GetImage(k),
                    elementText: k => OperationClient.GetText(k),
                    title: SelectorMessage.ConstructorSelector.NiceToString(),
                    message: SelectorMessage.PleaseSelectAConstructor.NiceToString(),
                    owner: win))
                    return null;
            }

            var pair = dic[selected];

            if (pair.OperationSettings != null && pair.OperationSettings.Constructor != null)
                return pair.OperationSettings.Constructor(pair.OperationInfo, win);
            else
                return Server.Return((IOperationServer s) => s.Construct(entityType, selected));
        }


        protected internal virtual IEnumerable<MenuItem> SearchControl_GetConstructorFromManyMenuItems(SearchControl sc)
        {
            if (sc.SelectedItems.IsNullOrEmpty())
                return null;

            var types = sc.SelectedItems.Select(a => a.EntityType).Distinct().ToList();

            return (from t in types
                    from oi in OperationInfos(t)
                    where oi.OperationType == OperationType.ConstructorFromMany
                    group new { t, oi } by oi.OperationSymbol into g
                    let os = GetSettings<ContextualOperationSettings>(g.Key)
                    let coc = new ContextualOperationContext
                    {
                        Entities = sc.SelectedItems,
                        SearchControl = sc,
                        OperationSettings = os,
                        OperationInfo = g.First().oi,
                        CanExecute = OperationSymbol.NotDefinedForMessage(g.Key, types.Except(g.Select(a => a.t)))
                    }
                    where os == null || os.IsVisible == null || os.IsVisible(coc)
                    select ConstructFromManyMenuItemConsturctor.Construct(coc))
                    .OrderBy(Common.GetOrder)
                   .ToList();
        }

        protected internal virtual IEnumerable<MenuItem> SearchControl_GetEntityOperationMenuItem(SearchControl sc)
        {
            if (sc.SelectedItems.IsNullOrEmpty() || sc.SelectedItems.Length != 1)
                return null;

            if (sc.Implementations.IsByAll)
                return null;

            var operations = (from oi in OperationInfos(sc.SelectedItem.EntityType)
                              where oi.IsEntityOperation
                              let os = GetSettings<EntityOperationSettings>(oi.OperationSymbol)
                              let coc = new ContextualOperationContext
                              {
                                  Entities = sc.SelectedItems,
                                  SearchControl = sc,
                                  OperationSettings = os == null ? null : os.Contextual,
                                  OperationInfo = oi,
                              }
                              where os == null ? oi.Lite == true :
                                    os.Contextual.IsVisible == null ? (oi.Lite == true && os.IsVisible == null && (os.Click == null || os.Contextual.Click != null)) :
                                    os.Contextual.IsVisible(coc)
                              select coc).ToList();

            if (operations.IsEmpty())
                return null;

            if (operations.Any(eomi => eomi.OperationInfo.HasCanExecute == true))
            {
                Dictionary<OperationSymbol, string> canExecutes = Server.Return((IOperationServer os) => os.GetCanExecuteLiteAll(sc.SelectedItem));
                foreach (var coc in operations)
                {
                    var ce = canExecutes.TryGetC(coc.OperationInfo.OperationSymbol);
                    if (ce != null && ce.HasText())
                        coc.CanExecute = ce;
                }
            }

            return operations.Select(coc => EntityOperationMenuItemConsturctor.Construct(coc)).OrderBy(Common.GetOrder);
        }


        static HashSet<Type> SaveProtectedCache;
        protected internal virtual bool SaveProtected(Type type)
        {
            if (!type.IsIIdentifiable())
                return false;

            if (SaveProtectedCache == null)
                SaveProtectedCache = Server.Return((IOperationServer o) => o.GetSaveProtectedTypes());

            return SaveProtectedCache.Contains(type);
        }
    }

    public class OperationColor
    {
        public OperationColor(Func<OperationInfo, bool> isApplicable)
        {
            IsApplicable = isApplicable;
        }
        public Func<OperationInfo, bool> IsApplicable;
        public Color Color;
    }
}
