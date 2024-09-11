﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WpfAnimatedGif;
using static BarbarianPrince.EventViewerE010Mgr;
using Point = System.Windows.Point;

namespace BarbarianPrince
{
   public partial class EventViewerE212CurseMgr : UserControl
   {
      public delegate bool EndCurseCallback();
      private const int STARTING_ASSIGNED_ROW = 6;
      private const int DO_NOT_LEAVE = -10;
      private const int LEAVE = 10;
      //---------------------------------------------
      public struct GridRow
      {
         public IMapItem myMapItem;
         public int myDieRoll;
         public GridRow(IMapItem mi)
         {
            myMapItem = mi;
            myDieRoll = Utilities.NO_RESULT;
         }
      };
      public enum E212Enum
      {
         CURSE_CHECK,
         SHOW_RESULTS,
         END
      };
      //---------------------------------------------
      public bool CtorError { get; } = false;
      //---------------------------------------------
      private E212Enum myState = E212Enum.CURSE_CHECK;
      private EndCurseCallback myCallback = null;
      private int myMaxRowCount = 0;
      private GridRow[] myGridRows = null;
      private int myNumTrueLove = 0; // if number of true loves is greater than one, it is a triangle. True Loves can leave.
      //---------------------------------------------
      private IGameInstance myGameInstance = null;
      private readonly Canvas myCanvas = null;
      private readonly ScrollViewer myScrollViewer = null;
      private RuleDialogViewer myRulesMgr = null;
      //---------------------------------------------
      private IDieRoller myDieRoller = null;
      private int myRollResultRowNum = 0;
      private bool myIsRollInProgress = false;
      //---------------------------------------------
      private readonly SolidColorBrush mySolidColorBrushBlack = new SolidColorBrush() { Color = Colors.Black };
      private readonly FontFamily myFontFam = new FontFamily("Tahoma");
      private readonly FontFamily myFontFam1 = new FontFamily("Courier New");
      //-----------------------------------------------------------------------------------------
      public EventViewerE212CurseMgr(IGameInstance gi, Canvas c, ScrollViewer sv, RuleDialogViewer rdv, IDieRoller dr)
      {
         InitializeComponent();
         //--------------------------------------------------
         if (null == gi) // check parameter inputs
         {
            Logger.Log(LogEnum.LE_ERROR, "EventViewerE212CurseMgr(): gi=null");
            CtorError = true;
            return;
         }
         myGameInstance = gi;
         //--------------------------------------------------
         if (null == c) // check parameter inputs
         {
            Logger.Log(LogEnum.LE_ERROR, "EventViewerE212CurseMgr(): c=null");
            CtorError = true;
            return;
         }
         myCanvas = c;
         //--------------------------------------------------
         if (null == sv)
         {
            Logger.Log(LogEnum.LE_ERROR, "EventViewerE212CurseMgr(): sv=null");
            CtorError = true;
            return;
         }
         myScrollViewer = sv;
         //--------------------------------------------------
         if (null == rdv)
         {
            Logger.Log(LogEnum.LE_ERROR, "EventViewerE212CurseMgr(): rdv=null");
            CtorError = true;
            return;
         }
         myRulesMgr = rdv;
         //--------------------------------------------------
         if (null == dr)
         {
            Logger.Log(LogEnum.LE_ERROR, "EventViewerE212CurseMgr(): dr=true");
            CtorError = true;
            return;
         }
         myDieRoller = dr;
         //--------------------------------------------------
         myGrid.MouseDown += Grid_MouseDown;
      }
      public bool CurseCheck(EndCurseCallback callback)
      {
         //--------------------------------------------------
         if (null == myGameInstance.PartyMembers)
         {
            Logger.Log(LogEnum.LE_ERROR, "CurseCheck(): partyMembers=null");
            return false;
         }
         if (myGameInstance.PartyMembers.Count < 1) // at a minimum, the prince needs to be in party
         {
            Logger.Log(LogEnum.LE_ERROR, "CurseCheck(): myGameInstance.PartyMembers.Count < 1");
            return false;
         }
         //--------------------------------------------------
         myGridRows = new GridRow[Utilities.MAX_GRID_ROW];
         myState = E212Enum.CURSE_CHECK;
         myMaxRowCount = myGameInstance.PartyMembers.Count;
         myIsRollInProgress = false;
         myRollResultRowNum = 0;
         myCallback = callback;
         myNumTrueLove = 0;
         //--------------------------------------------------
         int i = 0;
         foreach (IMapItem mi in myGameInstance.PartyMembers)
         {
            if (null == mi)
            {
               Logger.Log(LogEnum.LE_ERROR, "CurseCheck(): mi=null");
               return false;
            }
            myGridRows[i] = new GridRow(mi);
            if ("Prince" == mi.Name)
               myGridRows[i].myDieRoll = DO_NOT_LEAVE;
            else if (true == mi.Name.Contains("PorterSlave"))
               myGridRows[i].myDieRoll = DO_NOT_LEAVE;
            else if (true == myGameInstance.IsMinstrelPlaying)
               myGridRows[i].myDieRoll = DO_NOT_LEAVE;
            else if (true == mi.Name.Contains("TrueLove")) // if there is more than one true love, all but one may leave
               ++myNumTrueLove;
            ++i;
         }
         if (1 == myNumTrueLove)
         {
            for (int k = 0; k < myMaxRowCount; ++k)
            {
               IMapItem mi = myGridRows[k].myMapItem;
               if (true == mi.Name.Contains("TrueLove")) // if there is more than one true love, all but one may leave
                  myGridRows[k].myDieRoll = DO_NOT_LEAVE;
            }
         }
         //--------------------------------------------------
         myState = E212Enum.END; // end this dialog if nobody checks for curses
         for (int k = 0; k < myMaxRowCount; ++k)
         {
            if (DO_NOT_LEAVE == myGridRows[k].myDieRoll)
               myState = E212Enum.CURSE_CHECK;
         }
         //--------------------------------------------------
         // Add the unassignable mapitems that never move or change to the Grid Rows
         if (false == UpdateGrid())
         {
            Logger.Log(LogEnum.LE_ERROR, "CurseCheck(): UpdateGrid() return false");
            return false;
         }
         myScrollViewer.Content = myGrid;
         return true;
      }
      //-----------------------------------------------------------------------------------------
      private bool UpdateGrid()
      {
         if (false == UpdateEndState())
         {
            Logger.Log(LogEnum.LE_ERROR, "UpdateGrid(): UpdateEndState() returned false");
            return false;
         }
         if (E212Enum.END == myState)
            return true;
         if (false == UpdateUserInstructions())
         {
            Logger.Log(LogEnum.LE_ERROR, "UpdateGrid(): UpdateUserInstructions() returned false");
            return false;
         }
         if (false == UpdateAssignablePanel())
         {
            Logger.Log(LogEnum.LE_ERROR, "UpdateGrid(): UpdateAssignablePanel() returned false");
            return false;
         }
         if (false == UpdateGridRows())
         {
            Logger.Log(LogEnum.LE_ERROR, "UpdateGrid(): UpdateGridRows() returned false");
            return false;
         }
         return true;
      }
      private bool UpdateEndState()
      {
         if (E212Enum.END == myState)
         {
            if (null == myCallback)
            {
               Logger.Log(LogEnum.LE_ERROR, "UpdateEndState(): myCallback=null");
               return false;
            }
            //---------------------------------
            bool isAnyMemberLeaving = false;
            for (int i = 0; i < myMaxRowCount; ++i) // Remove all members that deserted
            {
               if (1 < myGridRows[i].myDieRoll)
               {
                  myGameInstance.RemoveAbandonerInParty(myGridRows[i].myMapItem, true);
                  isAnyMemberLeaving = true;
               }
            }
            //---------------------------------
            if (true == isAnyMemberLeaving) // If any member leaves, all fickle members leave
            {
               IMapItems fickleMembers = new MapItems();
               foreach (IMapItem mi in myGameInstance.PartyMembers)
               {
                  if (true == mi.IsFickle)
                     fickleMembers.Add(mi);
               }
               foreach (IMapItem mi in fickleMembers)
                  myGameInstance.RemoveAbandonerInParty(mi);
            }
            //---------------------------------
            if (false == myCallback())
            {
               Logger.Log(LogEnum.LE_ERROR, "UpdateEndState(): myCallback() returned false");
               return false;
            }
         }
         return true;
      }
      private bool UpdateUserInstructions()
      {
         myTextBlockInstructions.Inlines.Clear();
         switch (myState)
         {
            case E212Enum.CURSE_CHECK:
               if ((false == myGameInstance.IsMinstrelPlaying) && (true == myGameInstance.IsMinstrelInParty()))
                  myTextBlockInstructions.Inlines.Add(new Run("Click minstrel or roll for desertion due to curse."));
               else
                  myTextBlockInstructions.Inlines.Add(new Run("Roll for desertion due to temple curse."));
               break;
            case E212Enum.SHOW_RESULTS:
               myTextBlockInstructions.Inlines.Add(new Run("Click campfire to continue."));
               break;
            default:
               Logger.Log(LogEnum.LE_ERROR, "UpdateUserInstructions(): reached default" + myState.ToString());
               return false;
         }
         return true;
      }
      private bool UpdateAssignablePanel()
      {
         myStackPanelAssignable.Children.Clear(); // clear out assignable panel 
         switch (myState)
         {
            case E212Enum.CURSE_CHECK:
               if ((false == myGameInstance.IsMinstrelPlaying) && (true == myGameInstance.IsMinstrelInParty()))
               {
                  Image img3 = new Image { Source = MapItem.theMapImages.GetBitmapImage("c60Minstrel"), Name = "MinstrelStart", Width = Utilities.ZOOM * Utilities.theMapItemSize, Height = Utilities.ZOOM * Utilities.theMapItemSize };
                  myStackPanelAssignable.Children.Add(img3);
               }
               else
               {
                  Rectangle r0 = new Rectangle() { Visibility = Visibility.Hidden, Width = Utilities.ZOOM * Utilities.theMapItemSize, Height = Utilities.ZOOM * Utilities.theMapItemSize };
                  myStackPanelAssignable.Children.Add(r0);
               }
               break;
            case E212Enum.SHOW_RESULTS:
               BitmapImage bmi1 = new BitmapImage();
               bmi1.BeginInit();
               bmi1.UriSource = new Uri(MapImage.theImageDirectory + "Campfire2.gif", UriKind.Absolute);
               bmi1.EndInit();
               Image img1 = new Image { Name = "Campfire", Source = bmi1, Width = Utilities.ZOOM * Utilities.theMapItemSize, Height = Utilities.ZOOM * Utilities.theMapItemSize };
               ImageBehavior.SetAnimatedSource(img1, bmi1);
               myStackPanelAssignable.Children.Add(img1);
               break;
            default:
               Logger.Log(LogEnum.LE_ERROR, "UpdateAssignablePanel(): reached default s=" + myState.ToString());
               return false;
         }
         return true;
      }
      private bool UpdateGridRows()
      {
         //------------------------------------------------------------
         // Clear out existing Grid Row data
         List<UIElement> results = new List<UIElement>();
         foreach (UIElement ui in myGrid.Children)
         {
            int rowNum = Grid.GetRow(ui);
            if (STARTING_ASSIGNED_ROW <= rowNum)
               results.Add(ui);
         }
         foreach (UIElement ui1 in results)
            myGrid.Children.Remove(ui1);
         //------------------------------------------------------------
         for (int i = 0; i < myMaxRowCount; ++i)
         {
            int rowNum = i + STARTING_ASSIGNED_ROW;
            IMapItem mi = myGridRows[i].myMapItem;
            //------------------------------------
            Button b = CreateButton(mi);
            myGrid.Children.Add(b);
            Grid.SetRow(b, rowNum);
            Grid.SetColumn(b, 0);
            //--------------------------------
            if (DO_NOT_LEAVE == myGridRows[i].myDieRoll)
            {
               Label label = new Label() { FontFamily = myFontFam, FontSize = 24, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Content = "NA" };
               myGrid.Children.Add(label);
               Grid.SetRow(label, rowNum);
               Grid.SetColumn(label, 1);
               Label labelResult = new Label() { FontFamily = myFontFam, FontSize = 24, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Content = "NA" };
               myGrid.Children.Add(labelResult);
               Grid.SetRow(labelResult, rowNum);
               Grid.SetColumn(labelResult, 2);
               continue;
            }
            //--------------------------------
            if (Utilities.NO_RESULT == myGridRows[i].myDieRoll)
            {
               BitmapImage bmi = new BitmapImage();
               bmi.BeginInit();
               bmi.UriSource = new Uri(MapImage.theImageDirectory + "dieRoll.gif", UriKind.Absolute);
               bmi.EndInit();
               Image img = new Image { Source = bmi, Width = Utilities.theMapItemOffset, Height = Utilities.theMapItemOffset };
               ImageBehavior.SetAnimatedSource(img, bmi);
               myGrid.Children.Add(img);
               Grid.SetRow(img, rowNum);
               Grid.SetColumn(img, 1);
            }
            else
            {
               string dieRollLabel = myGridRows[i].myDieRoll.ToString();
               if (LEAVE == myGridRows[i].myDieRoll)
                  dieRollLabel = "NA";
               Label label = new Label() { FontFamily = myFontFam, FontSize = 24, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Content = dieRollLabel };
               myGrid.Children.Add(label);
               Grid.SetRow(label, rowNum);
               Grid.SetColumn(label, 1);
               //-------------------------------
               Label labelResult = new Label() { FontFamily = myFontFam, FontSize = 24, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
               myGrid.Children.Add(labelResult);
               Grid.SetRow(labelResult, rowNum);
               Grid.SetColumn(labelResult, 2);
               if (1 < myGridRows[i].myDieRoll)
                  labelResult.Content = "yes";
               else
                  labelResult.Content = "no";
            }
         }
         return true;
      }
      //-----------------------------------------------------------------------------------------
      private Button CreateButton(IMapItem mi)
      {
         System.Windows.Controls.Button b = new Button { };
         b.Name = Utilities.RemoveSpaces(mi.Name);
         b.Width = Utilities.ZOOM * Utilities.theMapItemSize;
         b.Height = Utilities.ZOOM * Utilities.theMapItemSize;
         b.BorderThickness = new Thickness(1);
         b.BorderBrush = Brushes.Black;
         b.Background = new SolidColorBrush(Colors.Transparent);
         b.Foreground = new SolidColorBrush(Colors.Transparent);
         MapItem.SetButtonContent(b, mi, false, true); // This sets the image as the button's content
         return b;
      }
      public void ShowDieResults(int dieRoll)
      {
         int i = myRollResultRowNum - STARTING_ASSIGNED_ROW;
         if (i < 0)
         {
            Logger.Log(LogEnum.LE_ERROR, "ShowDieResults(): 0 > i=" + i.ToString() + " myState=" + myState.ToString());
            return;
         }
         IMapItem mi = myGridRows[i].myMapItem;
         myGridRows[i].myDieRoll = dieRoll;
         bool isMemberLeaving = false;
         if (1 < myGridRows[i].myDieRoll)
         {
            isMemberLeaving = true;
            myGridRows[i].myMapItem.OverlayImageName = "OMIA";
            if (true == mi.Name.Contains("TrueLove"))
            {
               --myNumTrueLove;
               if (1 == myNumTrueLove)  // If down to one true love, she does not leave
               {
                  for (int k = 0; k < myMaxRowCount; ++k)
                  {
                     IMapItem mi1 = myGridRows[k].myMapItem;
                     if ((true == mi1.Name.Contains("TrueLove")) && (Utilities.NO_RESULT == myGridRows[k].myDieRoll))
                        myGridRows[k].myDieRoll = DO_NOT_LEAVE;
                  }
               }
            }
         }
         //-----------------------------------------------------------------
         if (true == isMemberLeaving)
         {
            for (int k = 0; k < myMaxRowCount; ++k)
            {
               IMapItem mi1 = myGridRows[k].myMapItem;
               if (true == mi1.IsFickle)
                  myGridRows[k].myDieRoll = LEAVE;
            }
         }
         //-----------------------------------------------------------------
         myState = E212Enum.SHOW_RESULTS;
         for (int j = 0; j < myMaxRowCount; ++j)
         {
            IMapItem mi1 = myGridRows[j].myMapItem;
            if (Utilities.NO_RESULT == myGridRows[j].myDieRoll)
               myState = E212Enum.CURSE_CHECK;
         }
         //-----------------------------------------------------------------
         if (false == UpdateGrid())
            Logger.Log(LogEnum.LE_ERROR, "ShowDieResults(): UpdateGrid() return false");
         myIsRollInProgress = false;
      }
      //-----------------------------------------------------------------------------------------
      private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
      {
         System.Windows.Point p = e.GetPosition((UIElement)sender);
         HitTestResult result = VisualTreeHelper.HitTest(myGrid, p);  // Get the Point where the hit test occurrs
         foreach (UIElement ui in myGrid.Children)
         {
            if (ui is StackPanel panel)
            {
               foreach (UIElement ui1 in panel.Children)
               {
                  if (ui1 is Image img0) // Check all images within the myStackPanelAssignable
                  {
                     if (result.VisualHit == img0)
                     {
                        if ("Campfire" == img0.Name)
                        {
                           myState = E212Enum.END;
                           if (false == UpdateGrid())
                              Logger.Log(LogEnum.LE_ERROR, "Grid_MouseDown(): UpdateGrid() return false");
                           return;
                        }
                        else if ("MinstrelStart" == img0.Name)
                        {
                           myGameInstance.MinstrelStart();
                           for (int k = 0; k < myMaxRowCount; ++k)
                              myGridRows[k].myDieRoll = DO_NOT_LEAVE;
                           myState = E212Enum.SHOW_RESULTS;
                           if (false == UpdateGrid())
                              Logger.Log(LogEnum.LE_ERROR, "Grid_MouseDown(): UpdateGrid() return false");
                           return;
                        }
                     }
                  }
               }
            }
            if (ui is Image img1) // next check all images within the Grid Rows
            {
               if (result.VisualHit == img1)
               {
                  if (false == myIsRollInProgress)
                  {
                     myRollResultRowNum = Grid.GetRow(img1);  // select the row number of the opener
                     myIsRollInProgress = true;
                     RollEndCallback callback = ShowDieResults;
                     myDieRoller.RollMovingDie(myCanvas, callback);
                     img1.Visibility = Visibility.Hidden;
                  }
                  return;
               }
            }
         }
      }
      private void ButtonRule_Click(object sender, RoutedEventArgs e)
      {
         Button b = (Button)sender;
         String content = (String)b.Content;
         if (null == myRulesMgr)
            Logger.Log(LogEnum.LE_ERROR, "ButtonRule_Click(): myRulesMgr=null");
         else if (false == myRulesMgr.ShowRule(content))
            Logger.Log(LogEnum.LE_ERROR, "ButtonRule_Click(): myRulesMgr.ShowRule() returned false for c=" + content);
      }
   }
}
