﻿/* Copyright (c) Citrix Systems Inc. 
 * All rights reserved. 
 * 
 * Redistribution and use in source and binary forms, 
 * with or without modification, are permitted provided 
 * that the following conditions are met: 
 * 
 * *   Redistributions of source code must retain the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer. 
 * *   Redistributions in binary form must reproduce the above 
 *     copyright notice, this list of conditions and the 
 *     following disclaimer in the documentation and/or other 
 *     materials provided with the distribution. 
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND 
 * CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
 * INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF 
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE 
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR 
 * CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, 
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, 
 * BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR 
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
 * WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE 
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF 
 * SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using XenAdmin.Actions;
using XenAdmin.Controls;
using XenAdmin.Core;
using XenAdmin.Dialogs;
using XenAPI;


namespace XenAdmin.TabPages
{
    public partial class HistoryPage : UserControl
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const int MAX_HISTORY_ITEM = 1000;

        internal event Action<IXenObject> GoToXenObjectRequested;

        public HistoryPage()
        {
            InitializeComponent();
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            dataGridView.Sort(columnDate, ListSortDirection.Descending);
            toolStripTop.Renderer = new CustomToolStripRenderer();
            toolStripSplitButtonDismiss.DefaultItem = tsmiDismissAll;
            toolStripSplitButtonDismiss.Text = tsmiDismissAll.Text;
            UpdateButtons();
            ConnectionsManager.History.CollectionChanged += History_CollectionChanged;
            ActionBase.NewAction += Action_NewAction;
        }

        public void RefreshDisplayedEvents()
        {
            toolStripDdbFilterLocation.InitializeHostList();
            toolStripDdbFilterLocation.BuildFilterList();
            BuildRowList();
        }

        private void Action_NewAction(ActionBase action)
        {
            if (action == null)
                return;

            Program.BeginInvoke(Program.MainWindow,
                           () =>
                           {
                               int count = ConnectionsManager.History.Count;
                               if (count >= MAX_HISTORY_ITEM)
                                   ConnectionsManager.History.RemoveAt(0);
                               ConnectionsManager.History.Add(action);
                           });
        }

        private void History_CollectionChanged(object sender, CollectionChangeEventArgs e)
        {
            Program.BeginInvoke(this, () =>
            {
                ActionBase action = (ActionBase)e.Element;
                switch (e.Action)
                {
                    case CollectionChangeAction.Add:
                        var actions = new List<ActionBase>(ConnectionsManager.History);
                        actions.RemoveAll(FilterAction);
                        SortActions(actions);
                        var index = actions.IndexOf(action);
                        if (index > -1)
                            InsertActionRow(index, action);
                        break;
                    case CollectionChangeAction.Remove:
                        RemoveActionRow(action);
                        break;
                    case CollectionChangeAction.Refresh:
                        BuildRowList();
                        break;
                }
                UpdateButtons();
            });
        }

        private void action_Changed(ActionBase sender)
        {
            var asyncAction = sender as AsyncAction;
            if (asyncAction != null)
                asyncAction.RecomputeCanCancel();

            Program.Invoke(this, () =>
                {
                    var row = FindRowFromAction(sender);
                    if (row != null)
                        row.RefreshSelf();

                    UpdateButtons();
                });
        }

        private void SetFilterLabel()
        {
            bool filterIsOn = toolStripDdbFilterDates.FilterIsOn
                              || toolStripDdbFilterLocation.FilterIsOn
                              || toolStripDdbFilterStatus.FilterIsOn;

            toolStripLabelFiltersOnOff.Text = filterIsOn
                                                  ? Messages.FILTERS_ON
                                                  : Messages.FILTERS_OFF;
        }

        private void BuildRowList()
        {
            if (!Visible)
                return;

            try
            {
                dataGridView.SuspendLayout();
                dataGridView.Rows.Clear();

                var actions = new List<ActionBase>(ConnectionsManager.History);
                actions.RemoveAll(FilterAction);
                SortActions(actions);
                
                var rows = new List<DataGridViewActionRow>();
                foreach (ActionBase action in actions)
                {
                    var row = CreateActionRow(action);
                    rows.Add(row);
                }
                dataGridView.Rows.AddRange(rows.ToArray());

                foreach(var actionRow in rows)
                    RegisterActionEvents(actionRow.Action);

                SetFilterLabel();
            }
            finally
            {
                dataGridView.ResumeLayout();
            }
        }

        private void SortActions(List<ActionBase> actions)
        {
            if (dataGridView.SortedColumn != null)
            {
                if (dataGridView.SortedColumn.Index == columnStatus.Index)
                    actions.Sort(ActionBaseExtensions.CompareOnStatus);
                else if (dataGridView.SortedColumn.Index == columnMessage.Index)
                    actions.Sort(ActionBaseExtensions.CompareOnTitle);
                else if (dataGridView.SortedColumn.Index == columnLocation.Index)
                    actions.Sort(ActionBaseExtensions.CompareOnLocation);
                else if (dataGridView.SortedColumn.Index == columnDate.Index)
                    actions.Sort(ActionBaseExtensions.CompareOnDateStarted);

                if (dataGridView.SortOrder == SortOrder.Descending)
                    actions.Reverse();
            }
        }

        private bool FilterAction(ActionBase action)
        {
            bool hide = false;
            Program.Invoke(this, () =>
                                 hide = toolStripDdbFilterDates.HideByDate(action.Started)
                                        || toolStripDdbFilterLocation.HideByLocation(action.GetApplicableHosts())
                                        || toolStripDdbFilterStatus.HideByStatus(action));
            return hide;
        }

        private void InsertActionRow(int index, ActionBase action)
        {
            if (index < 0)
                index = 0;
            if (index > dataGridView.RowCount)
                index = dataGridView.RowCount;

            var row = CreateActionRow(action);
            dataGridView.Rows.Insert(index, row);
            RegisterActionEvents(action);
        }

        private DataGridViewActionRow CreateActionRow(ActionBase action)
        {
            var row = new DataGridViewActionRow(action);
            row.DismissalRequested += row_DismissalRequested;
            row.GoToXenObjectRequested += row_GoToXenObjectRequested;
            return row;
        }

        private void RegisterActionEvents(ActionBase action)
        {
            action.Changed -= action_Changed;
            action.Completed -= action_Changed;
            action.Changed += action_Changed;
            action.Completed += action_Changed;
        }

        private void RemoveActionRow(ActionBase action)
        {
            var row = FindRowFromAction(action);
            if (row != null)
            {
                action.Changed -= action_Changed;
                action.Completed -= action_Changed;
                dataGridView.Rows.Remove(row);
            }
        }

        private DataGridViewActionRow FindRowFromAction(ActionBase action)
        {
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                var actionRow = row as DataGridViewActionRow;
                if (actionRow == null)
                    continue;

                if (actionRow.Action == action)
                    return actionRow;
            }

            return null;
        }

        private void ToggleExpandedState(int rowIndex)
        {
            var row = dataGridView.Rows[rowIndex] as DataGridViewActionRow;
            if (row == null)
                return;

            if (row.Expanded)
            {
                row.Cells[columnExpander.Index].Value = Properties.Resources.contracted_triangle;
                row.Cells[columnMessage.Index].Value = row.Action.GetTitle();
            }
            else
            {
                row.Cells[columnExpander.Index].Value = Properties.Resources.expanded_triangle;
                row.Cells[columnMessage.Index].Value = row.Action.GetDetails();
            }
            row.Expanded = !row.Expanded;
        }

        private void UpdateButtons()
        {
            toolStripDdbFilterStatus.Enabled =
                toolStripDdbFilterLocation.Enabled =
                toolStripDdbFilterDates.Enabled = ConnectionsManager.History.Count > 0;

            tsmiDismissAll.Enabled = dataGridView.Rows.Cast<DataGridViewActionRow>().Any(row => row.Action.IsCompleted);
            tsmiDismissSelected.Enabled = dataGridView.SelectedRows.Cast<DataGridViewActionRow>().Any(row => row.Action.IsCompleted);
            toolStripSplitButtonDismiss.Enabled = tsmiDismissAll.Enabled || tsmiDismissSelected.Enabled;

            if (toolStripSplitButtonDismiss.DefaultItem != null && !toolStripSplitButtonDismiss.DefaultItem.Enabled)
            {
                foreach (ToolStripItem item in toolStripSplitButtonDismiss.DropDownItems)
                {
                    if (item.Enabled)
                    {
                        toolStripSplitButtonDismiss.DefaultItem = item;
                        toolStripSplitButtonDismiss.Text = item.Text;
                        break;
                    }
                }
            }
        }

        #region Control event handlers

        private void row_DismissalRequested(DataGridViewActionRow row)
        {
            if (ConnectionsManager.History.Count > 0 &&
                (Program.RunInAutomatedTestMode ||
                 new ThreeButtonDialog(
                     new ThreeButtonDialog.Details(null, Messages.MESSAGEBOX_LOG_DELETE, Messages.MESSAGEBOX_LOG_DELETE_TITLE),
                     ThreeButtonDialog.ButtonYes,
                     ThreeButtonDialog.ButtonNo).ShowDialog(this) == DialogResult.Yes))
            {
                ConnectionsManager.History.Remove(row.Action);   
            }
        }

        private void row_GoToXenObjectRequested(IXenObject obj)
        {
            if (GoToXenObjectRequested != null)
                GoToXenObjectRequested(obj);
        }


        private void dataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            // If you click on the headers you can get -1 as the index.
            if (e.ColumnIndex < 0 || e.RowIndex < 0 || e.ColumnIndex != columnExpander.Index)
                return;

            ToggleExpandedState(e.RowIndex);
        }

        private void dataGridView_SelectionChanged(object sender, EventArgs e)
        {
            tsmiDismissSelected.Enabled = dataGridView.SelectedRows.Cast<DataGridViewActionRow>().Any(row => row.Action.IsCompleted);
        }

        private void dataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (dataGridView.Columns[e.ColumnIndex].SortMode == DataGridViewColumnSortMode.Automatic)
                BuildRowList();
        }


        private void toolStripSplitButtonDismiss_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            toolStripSplitButtonDismiss.DefaultItem = e.ClickedItem;
            toolStripSplitButtonDismiss.Text = toolStripSplitButtonDismiss.DefaultItem.Text;
        }

        private void tsmiDismissAll_Click(object sender, EventArgs e)
        {
            if (ConnectionsManager.History.Count > 0 &&
                (Program.RunInAutomatedTestMode ||
                 new ThreeButtonDialog(
                     new ThreeButtonDialog.Details(null, Messages.MESSAGEBOX_LOGS_DELETE, Messages.MESSAGEBOX_LOGS_DELETE_TITLE),
                     ThreeButtonDialog.ButtonYes,
                     ThreeButtonDialog.ButtonNo).ShowDialog(this) == DialogResult.Yes))
            {
                ConnectionsManager.History.RemoveAll(a => a.IsCompleted);
            }
        }

        private void tsmiDismissSelected_Click(object sender, EventArgs e)
        {
            using (var dlog = new ThreeButtonDialog(
                    new ThreeButtonDialog.Details(null, Messages.MESSAGEBOX_LOGS_DELETE_SELECTED, Messages.MESSAGEBOX_LOGS_DELETE_TITLE),
                    ThreeButtonDialog.ButtonYes,
                    ThreeButtonDialog.ButtonNo))
            {

                if (dlog.ShowDialog(this) != DialogResult.Yes)
                    return;
            }

            var actions = new List<ActionBase>();
            foreach (var row in dataGridView.SelectedRows)
            {
                var actionRow = row as DataGridViewActionRow;
                if (actionRow != null)
                    actions.Add(actionRow.Action);
            }

            ConnectionsManager.History.RemoveAll(actions.Contains);
        }


        private void toolStripDdbFilterStatus_FilterChanged()
        {
            BuildRowList();
        }

        private void toolStripDdbFilterLocation_FilterChanged()
        {
            BuildRowList();
        }

        private void toolStripDdbFilterDates_FilterChanged()
        {
            BuildRowList();
        }

        #endregion

        #region Nested classes

        public class DataGridViewActionRow : DataGridViewRow
        {
            private DataGridViewImageCell expanderCell = new DataGridViewImageCell();
            private DataGridViewImageCell statusCell = new DataGridViewImageCell();
            private DataGridViewTextBoxCell messageCell = new DataGridViewTextBoxCell();
            private DataGridViewTextBoxCell locationCell = new DataGridViewTextBoxCell();
            private DataGridViewTextBoxCell dateCell = new DataGridViewTextBoxCell();
            private DataGridViewDropDownSplitButtonCell actionCell = new DataGridViewDropDownSplitButtonCell();

            private ToolStripMenuItem cancelItem = new ToolStripMenuItem(Messages.CANCEL);
            private ToolStripMenuItem dismissItem = new ToolStripMenuItem(Messages.ALERT_DISMISS);
            private ToolStripMenuItem goToItem = new ToolStripMenuItem(Messages.HISTORYPAGE_GOTO);
            private ToolStripSeparator separatorItem = new ToolStripSeparator();
            private ToolStripMenuItem copyItem = new ToolStripMenuItem(Messages.COPY);

            public event Action<DataGridViewActionRow> DismissalRequested;
            public event Action<IXenObject> GoToXenObjectRequested;

            public ActionBase Action { get; private set; }
            public bool Expanded { get; set; }

            public DataGridViewActionRow(ActionBase action)
            {
                Action = action;

                cancelItem.Click += ToolStripMenuItemCancel_Click;
                dismissItem.Click += ToolStripMenuItemDismiss_Click;
                goToItem.Click += ToolStripMenuItemGoTo_Click;
                copyItem.Click += ToolStripMenuItemCopy_Click;

                MinimumHeight = DataGridViewDropDownSplitButtonCell.MIN_ROW_HEIGHT;
                Cells.AddRange(expanderCell, statusCell, messageCell, locationCell, dateCell, actionCell);
                RefreshSelf();
            }

            public void RefreshSelf()
            {
                var actionItems = new List<ToolStripItem>();
                if (!Action.IsCompleted && Action.CanCancel)
                    actionItems.Add(cancelItem);

                if (Action.IsCompleted)
                    actionItems.Add(dismissItem);

                var obj = Action.GetRelevantXenObject();
                if (obj != null)
                    actionItems.Add(goToItem);

                if (actionItems.Count > 0)
                    actionItems.Add(separatorItem);

                actionItems.Add(copyItem);

                actionCell.RefreshItems(actionItems.ToArray());

                statusCell.Value = Action.GetImage();

                if (Expanded)
                {
                    expanderCell.Value = Properties.Resources.expanded_triangle;
                    messageCell.Value = Action.GetDetails();
                }
                else
                {
                    expanderCell.Value = Properties.Resources.contracted_triangle;
                    messageCell.Value = Action.GetTitle();
                }
                locationCell.Value = Action.GetLocation();
                dateCell.Value = HelpersGUI.DateTimeToString(Action.Started.ToLocalTime(), Messages.DATEFORMAT_DMY_HM, true);
            }

            private void ToolStripMenuItemCancel_Click(object sender, EventArgs e)
            {
                Action.Cancel();
            }

            private void ToolStripMenuItemDismiss_Click(object sender, EventArgs e)
            {
                if (DismissalRequested != null)
                    DismissalRequested(this);
            }

            private void ToolStripMenuItemGoTo_Click(object sender, EventArgs e)
            {
                if (GoToXenObjectRequested != null)
                    GoToXenObjectRequested(Action.GetRelevantXenObject());
            }

            private void ToolStripMenuItemCopy_Click(object sender, EventArgs e)
            {
                string text = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\"",
                                Action.GetStatusString(), messageCell.Value,
                                locationCell.Value, dateCell.Value);

                try
                {
                    Clipboard.SetText(text);
                }
                catch (Exception ex)
                {
                    log.Error("Exception while trying to set clipboard text.", ex);
                    log.Error(ex, ex);
                }
            }
        }

        private class CustomToolStripRenderer : ToolStripProfessionalRenderer
        {
            public CustomToolStripRenderer()
                : base(new CustomToolstripColourTable())
            {
                RoundedEdges = false;
            }
        }

        private class CustomToolstripColourTable : ProfessionalColorTable
        {
            public override Color ToolStripBorder
            {
                get { return Color.Gainsboro; }
            }
        }

        #endregion
    }
}
