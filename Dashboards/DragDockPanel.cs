//-----------------------------------------------------------------------
// <copyright file="DragDockPanel.cs" company="Microsoft Corporation copyright 2008.">
// (c) 2008 Microsoft Corporation. All rights reserved.
// This source is subject to the Microsoft Public License.
// See http://www.microsoft.com/resources/sharedsource/licensingbasics/sharedsourcelicenses.mspx.
// </copyright>
// <date>15-Sep-2008</date>
// <author>Martin Grayson</author>
// <summary>A draggable, dockable, expandable panel class.</summary>
//-----------------------------------------------------------------------
namespace Blacklight.Controls
{
    using System;
    using System.Net;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Documents;
    using System.Windows.Ink;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using System.Windows.Shapes;
    using System.Windows.Controls.Primitives;

    /// <summary>
    /// A draggable, dockable, expandable panel class.
    /// </summary>
    public class DragDockPanel : DraggablePanel
    {
        /// <summary>
        /// The template part name for the maxmize toggle button.
        /// </summary>
        private const string ElementMaximizeToggleButton = "MaximizeToggleButton";

        #region Private members
        /// <summary>
        /// Ignore the last uncheck event flag.
        /// </summary>
        private bool ignoreCheckedChanged = false;

        /// <summary>
        /// Panel maximised flag.
        /// </summary>
        private PanelState panelState = PanelState.Restored;

        /// <summary>
        /// Stores the panel index.
        /// </summary>
        private int panelIndex = 0;
        #endregion
        
        /// <summary>
        /// Drag dock panel constructor.
        /// </summary>
        public DragDockPanel()
        {
            this.DefaultStyleKey = typeof(DragDockPanel);
        }

        #region Events
        /// <summary>
        /// The maxmised event.
        /// </summary>
        public event EventHandler Maximized;

        /// <summary>
        /// The restored event.
        /// </summary>
        public event EventHandler Restored;

        /// <summary>
        /// The minimized event.
        /// </summary>
        public event EventHandler Minimized;
        #endregion

        #region Public members
        /// <summary>
        /// Gets or sets the calculated panel index.
        /// </summary>
        public int PanelIndex
        {
            get { return this.panelIndex; }
            set { this.panelIndex = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether or not the panel is maximised.
        /// </summary>
        [System.ComponentModel.Category("Panel Properties"), System.ComponentModel.Description("Gets whether the panel is maximised.")]
        public PanelState PanelState
        {
            get 
            { 
                return this.panelState; 
            }

            set
            {
                this.panelState = value;

                switch (this.panelState)
                {
                    case PanelState.Restored:
                        this.Restore();
                        break;
                    case PanelState.Maximized:
                        this.Maximize();
                        break;
                    case PanelState.Minimized:
                        this.Minimize();
                        break;
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets called once the template is applied so we can fish out the bits
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ToggleButton maximizeToggle =
                this.GetTemplateChild(DragDockPanel.ElementMaximizeToggleButton) as ToggleButton;

            if (maximizeToggle != null)
            {
                maximizeToggle.Checked +=
                    new RoutedEventHandler(this.MaximizeToggle_Checked);
                maximizeToggle.Unchecked +=
                    new RoutedEventHandler(this.MaximizeToggle_Unchecked);

                if (this.PanelState == PanelState.Restored)
                {
                    this.ignoreCheckedChanged = maximizeToggle.IsChecked.Value; 
                    maximizeToggle.IsChecked = false;
                }
                else if (this.PanelState == PanelState.Maximized)
                {
                    maximizeToggle.IsChecked = true;
                }
            }
        }

        /// <summary>
        /// Override for updating the panel position.
        /// </summary>
        /// <param name="pos">The new position.</param>
        public override void UpdatePosition(Point pos)
        {
            Canvas.SetLeft(this, pos.X);
            Canvas.SetTop(this, pos.Y);
        }

        /// <summary>
        /// Override for when a panel is maximized.
        /// </summary>
        public virtual void Maximize()
        {
            // Bring the panel to the front
            Canvas.SetZIndex(this, CurrentZIndex++);

            bool raiseEvent = this.panelState != PanelState.Maximized;
            this.panelState = PanelState.Maximized;

            ToggleButton maximizeToggle =
                this.GetTemplateChild(DragDockPanel.ElementMaximizeToggleButton) as ToggleButton;

            if (maximizeToggle != null)
            {
                maximizeToggle.IsChecked = true;
            }

            // Fire the panel maximized event
            if (raiseEvent && this.Maximized != null)
            {
                this.Maximized(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Override for when the panel minimizes.
        /// </summary>
        public virtual void Minimize()
        {
        }

        /// <summary>
        /// Override for when the panel restores.
        /// </summary>
        public virtual void Restore()
        {
            this.panelState = PanelState.Restored;

            ToggleButton maximizeToggle =
                this.GetTemplateChild(DragDockPanel.ElementMaximizeToggleButton) as ToggleButton;

            if (maximizeToggle != null)
            {
                maximizeToggle.IsChecked = false;
            }

            // Fire the panel minimized event
            if (this.Restored != null)
            {
                this.Restored(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Minimize intneral method (for raise event).
        /// </summary>
        internal void MinimizeInternal()
        {
            this.Minimize();

            bool raiseEvent = this.panelState != PanelState.Minimized;
            this.panelState = PanelState.Minimized;

            ToggleButton maximizeToggle =
                this.GetTemplateChild(DragDockPanel.ElementMaximizeToggleButton) as ToggleButton;

            if (maximizeToggle != null)
            {
                this.ignoreCheckedChanged = maximizeToggle.IsChecked.Value;
                maximizeToggle.IsChecked = false;
            }

            // Fire the panel minimized event
            if (raiseEvent && this.Minimized != null)
            {
                this.Minimized(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Internal restore method (surpressing the checked changed event).
        /// </summary>
        internal void RestoreInternal()
        {
            this.ignoreCheckedChanged = true;

            this.panelState = PanelState.Restored;

            ToggleButton maximizeToggle =
                this.GetTemplateChild(DragDockPanel.ElementMaximizeToggleButton) as ToggleButton;

            if (maximizeToggle != null)
            {
                maximizeToggle.IsChecked = false;
            }
        }

        #region Maximize events
        /// <summary>
        /// Fires the minimised event.
        /// </summary>
        /// <param name="sender">The maximised toggle.</param>
        /// <param name="e">Routed event args.</param>
        private void MaximizeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.ignoreCheckedChanged)
            {
                this.Restore();
                this.ignoreCheckedChanged = false;
            }
            else
            {
                this.ignoreCheckedChanged = false;
            }
        }

        /// <summary>
        /// Fires the maximised event.
        /// </summary>
        /// <param name="sender">The maximised toggle.</param>
        /// <param name="e">Routed event args.</param>
        private void MaximizeToggle_Checked(object sender, RoutedEventArgs e)
        {
            this.Maximize();
        }
        #endregion
    }
}
