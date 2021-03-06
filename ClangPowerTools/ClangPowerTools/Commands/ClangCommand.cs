﻿using ClangPowerTools.Builder;
using ClangPowerTools.Events;
using ClangPowerTools.Script;
using ClangPowerTools.Services;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;

namespace ClangPowerTools
{
  public abstract class ClangCommand : BasicCommand
  {
    #region Members

    protected ItemsCollector mItemsCollector;
    protected FilePathCollector mFilePahtCollector;
    protected static RunningProcesses mRunningProcesses = new RunningProcesses();
    protected List<string> mDirectoriesPath = new List<string>();

    //private Commands2 mCommand;

    private const string kVs15Version = "2017";
    private Dictionary<string, string> mVsVersions = new Dictionary<string, string>
    {
      {"11.0", "2010"},
      {"12.0", "2012"},
      {"13.0", "2013"},
      {"14.0", "2015"},
      {"15.0", "2017"}
    };

    private bool mMissingLLVM = false;
    private IVsHierarchy mHierarchy;
    public event EventHandler<VsHierarchyDetectedEventArgs> HierarchyDetectedEvent;


    #endregion


    #region Properties


    protected string VsEdition { get; set; }
    protected string VsVersion { get; set; }
    protected string WorkingDirectoryPath { get; set; }
    protected IVsHierarchy ItemHierarchy
    {
      get => ItemHierarchy;
      set
      {
        if (null == value)
          return;
        mHierarchy = value;
        OnFileHierarchyChanged(new VsHierarchyDetectedEventArgs(mHierarchy));
      }
    }


    #endregion


    #region Constructor


    public ClangCommand(AsyncPackage aPackage, Guid aGuid, int aId)
        : base(aPackage, aGuid, aId)
    {

      if (VsServiceProvider.TryGetService(typeof(DTE), out object dte))
      {
        var dte2 = dte as DTE2;
        //mCommand = dte2.Commands as Commands2;
        VsEdition = dte2.Edition;
        mVsVersions.TryGetValue(dte2.Version, out string version);
        VsVersion = version;
      }
    }

    #endregion


    #region Methods

    #region Public Methods

    public void OnMissingLLVMDetected(object sender, MissingLlvmEventArgs e)
    {
      mMissingLLVM = e.MissingLLVM;
    }


    #endregion


    #region Protected methods

    protected void RunScript(int aCommandId)
    {
      try
      {
        var dte = VsServiceProvider.GetService(typeof(DTE)) as DTE2;
        dte.Solution.SaveAs(dte.Solution.FullName);

        IBuilder<string> runModeScriptBuilder = new RunModeScriptBuilder();
        runModeScriptBuilder.Build();
        var runModeParameters = runModeScriptBuilder.GetResult();

        IBuilder<string> genericScriptBuilder = new GenericScriptBuilder(VsEdition, VsVersion, aCommandId);
        genericScriptBuilder.Build();
        var genericParameters = genericScriptBuilder.GetResult();

        string solutionPath = dte.Solution.FullName;

        StatusBarHandler.Status(OutputWindowConstants.kCommandsNames[aCommandId] + " started...", 1, vsStatusAnimation.vsStatusAnimationBuild, 1);

        VsServiceProvider.TryGetService(typeof(SVsSolution), out object vsSolutionService);
        var vsSolution = vsSolutionService as IVsSolution;

        foreach (var item in mItemsCollector.GetItems)
        {
          IBuilder<string> itemRelatedScriptBuilder = new ItemRelatedScriptBuilder(item);
          itemRelatedScriptBuilder.Build();
          var itemRelatedParameters = itemRelatedScriptBuilder.GetResult();

          // From the first parameter is removed the last character which is mandatory "'"
          // and added to the end of the string to close the script
          var script = $"{runModeParameters.Remove(runModeParameters.Length - 1)} {itemRelatedParameters} {genericParameters}'";

          if (null != vsSolution)
            ItemHierarchy = AutomationUtil.GetItemHierarchy(vsSolution as IVsSolution, item);

          var process = PowerShellWrapper.Invoke(script, mRunningProcesses);

          if (mMissingLLVM)
            break;
        }
        StatusBarHandler.Status(OutputWindowConstants.kCommandsNames[aCommandId] + " finished", 0, vsStatusAnimation.vsStatusAnimationBuild, 0);
      }
      catch (Exception) { }
    }


    protected IEnumerable<IItem> CollectSelectedItems(bool aClangFormatFlag = false, List<string> aAcceptedExtensionTypes = null)
    {
      mItemsCollector = new ItemsCollector(aAcceptedExtensionTypes);
      mItemsCollector.CollectSelectedFiles(ActiveWindowProperties.GetProjectItemOfActiveWindow(), aClangFormatFlag);
      return mItemsCollector.GetItems;
    }


    #endregion


    #region Private Methods


    protected virtual void OnFileHierarchyChanged(VsHierarchyDetectedEventArgs e)
    {
      HierarchyDetectedEvent?.Invoke(this, e);
    }

    #endregion


    #endregion

  }
}
