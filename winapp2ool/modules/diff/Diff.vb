﻿'    Copyright (C) 2018-2019 Robbie Ward
' 
'    This file is a part of Winapp2ool
' 
'    Winapp2ool is free software: you can redistribute it and/or modify
'    it under the terms of the GNU General Public License as published by
'    the Free Software Foundation, either version 3 of the License, or
'    (at your option) any later version.
'
'    Winap2ool is distributed in the hope that it will be useful,
'    but WITHOUT ANY WARRANTY; without even the implied warranty of
'    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
'    GNU General Public License for more details.
'
'    You should have received a copy of the GNU General Public License
'    along with Winapp2ool.  If not, see <http://www.gnu.org/licenses/>.
Option Strict On
Imports System.IO

''' <summary>
''' A module whose purpose is to allow a user to perform a diff on two winapp2.ini files
''' </summary>
Module Diff
    ' File handlers
    Dim oldOrLocalFile As iniFile = New iniFile(Environment.CurrentDirectory, "winapp2.ini")
    Dim newOrRemoteFile As iniFile = New iniFile(Environment.CurrentDirectory, "")
    Dim logFile As iniFile = New iniFile(Environment.CurrentDirectory, "diff.txt")
    Dim outputToFile As String
    ' Module parameters
    Dim download As Boolean = False
    Dim saveLog As Boolean = False
    Dim settingsChanged As Boolean = False

    ''' <summary>
    ''' Handles the commandline args for Diff
    ''' </summary>
    '''  Diff args:
    ''' -d          : download the latest winapp2.ini
    ''' -ncc        : download the latest non-ccleaner winapp2.ini (implies -d)
    ''' -savelog    : save the diff.txt log
    Public Sub handleCmdLine()
        initDefaultSettings()
        handleDownloadBools(download)
        ' Make sure we have a name set for the new file if we're downloading or else the diff will not run
        If download Then newOrRemoteFile.name = If(remoteWinappIsNonCC, "Online non-ccleaner winapp2.ini", "Online winapp2.ini")
        invertSettingAndRemoveArg(saveLog, "-savelog")
        getFileAndDirParams(oldOrLocalFile, newOrRemoteFile, logFile)
        If Not newOrRemoteFile.name = "" Then initDiff()
    End Sub

    ''' <summary>
    ''' Restores the default state of the module's parameters
    ''' </summary>
    Private Sub initDefaultSettings()
        download = False
        logFile.resetParams()
        newOrRemoteFile.resetParams()
        oldOrLocalFile.resetParams()
        saveLog = False
        settingsChanged = False
    End Sub

    ''' <summary>
    ''' Runs the Differ from outside the module
    ''' </summary>
    ''' <param name="firstFile">The old winapp2.ini file</param>
    Public Sub remoteDiff(firstFile As iniFile, Optional dl As Boolean = True)
        download = dl
        oldOrLocalFile = firstFile
        initDiff()
    End Sub

    ''' <summary>
    ''' Prints the main menu to the user
    ''' </summary>
    Public Sub printMenu()
        Console.WindowHeight = If(settingsChanged, 32, 30)
        printMenuTop({"Observe the differences between two ini files"})
        print(1, "Run (default)", "Run the diff tool")
        print(0, "Select Older/Local File:", leadingBlank:=True)
        print(1, "File Chooser", "Choose a new name or location for your older ini file")
        print(0, "Select Newer/Remote File:", leadingBlank:=True)
        print(5, GetNameFromDL(True), "diffing against the latest winapp2.ini version on GitHub", cond:=Not isOffline, enStrCond:=download, leadingBlank:=True)
        print(1, "File Chooser", "Choose a new name or location for your newer ini file", Not download, isOffline, True)
        print(0, "Log Settings:")
        print(5, "Toggle Log Saving", "automatic saving of the Diff output", leadingBlank:=True, trailingBlank:=Not saveLog, enStrCond:=saveLog)
        print(1, "File Chooser (log)", "Change where Diff saves its log", saveLog, trailingBlank:=True)
        print(0, $"Older file: {replDir(oldOrLocalFile.path)}")
        print(0, $"Newer file: {If(newOrRemoteFile.name = "" And Not download, "Not yet selected", If(download, GetNameFromDL(True), replDir(newOrRemoteFile.path)))}", closeMenu:=Not saveLog And Not settingsChanged)
        print(0, $"Log   file: {replDir(logFile.path)}", cond:=saveLog, closeMenu:=Not settingsChanged)
        print(2, "Diff", cond:=settingsChanged, closeMenu:=True)
    End Sub

    ''' <summary>
    ''' Handles the user input from the main menu
    ''' </summary>
    ''' <param name="input">The String containing the user's input from the menu</param>
    Public Sub handleUserInput(input As String)
        Select Case True
            Case input = "0"
                exitModule()
            Case input = "1" Or input = ""
                If Not denyActionWithTopper(newOrRemoteFile.name = "" And Not download, "Please select a file against which to diff") Then initDiff()
            Case input = "2"
                changeFileParams(oldOrLocalFile, settingsChanged)
            Case input = "3" And Not isOffline
                toggleDownload(download, settingsChanged)
                newOrRemoteFile.name = GetNameFromDL(download)
            Case (input = "4" And Not (download Or isOffline)) Or (input = "3" And isOffline)
                changeFileParams(newOrRemoteFile, settingsChanged)
            Case (input = "5" And Not isOffline And Not download) Or (input = "4" And (isOffline Xor download))
                toggleSettingParam(saveLog, "Log Saving", settingsChanged)
            Case saveLog And ((input = "6" And Not isOffline And Not download) Or (input = "5" And (isOffline Or (Not isOffline And download))))
                changeFileParams(logFile, settingsChanged)
            Case settingsChanged And 'Online Case below
                (Not isOffline And (((Not saveLog And input = "5" And download) Or (input = "6" And Not (download Xor saveLog))) Or (input = "7" And Not download And saveLog))) Or
                ((isOffline) And (input = "5") Or (input = "6" And saveLog)) ' Offline case
                resetModuleSettings("Diff", AddressOf initDefaultSettings)
            Case Else
                setHeaderText(invInpStr, True)
        End Select
    End Sub

    ''' <summary>
    ''' Carries out the main set of Diffing operations
    ''' </summary>
    Private Sub initDiff()
        outputToFile = ""
        oldOrLocalFile.validate()
        If download Then newOrRemoteFile = getRemoteIniFile(getWinappLink)
        newOrRemoteFile.validate()
        If pendingExit() Then Exit Sub
        differ()
        If saveLog Then logFile.overwriteToFile(outputToFile)
        setHeaderText("Diff Complete")
    End Sub

    ''' <summary>
    ''' Gets the version from winapp2.ini
    ''' </summary>
    ''' <param name="someFile">winapp2.ini format iniFile object</param>
    ''' <returns></returns>
    Private Function getVer(someFile As iniFile) As String
        Dim ver = If(someFile.comments.Count > 0, someFile.comments(0).comment.ToString.ToLower, "000000")
        Return If(ver.Contains("version"), ver.TrimStart(CChar(";")).Replace("version:", "version"), " version not given")
    End Function

    ''' <summary>
    ''' Performs the diff and outputs the info to the user
    ''' </summary>
    Private Sub differ()
        print(3, "Diffing, please wait. This may take a moment.")
        clrConsole()
        Dim oldVersionNum = getVer(oldOrLocalFile)
        Dim newVersionNum = getVer(newOrRemoteFile)
        log(tmenu($"Changes made between{oldVersionNum} and{newVersionNum}"))
        log(menu(menuStr02))
        log(menu(menuStr00))
        ' Compare the files and then enumerate their changes
        Dim outList As List(Of String) = compareTo()
        Dim remCt = 0
        Dim modCt = 0
        Dim addCt = 0
        For Each change In outList
            Select Case True
                Case change.Contains("has been added")
                    addCt += 1
                Case change.Contains(" has been removed")
                    remCt += 1
                Case Else
                    modCt += 1
            End Select
            log(change)
        Next
        ' Print the summary to the user
        log(menu("Diff complete.", True))
        log(menu(menuStr03))
        log(menu("Summary", True))
        log(menu(menuStr01))
        log(menu($"Added entries: {addCt}"))
        log(menu($"Modified entries: {modCt}"))
        log(menu($"Removed entries: {remCt}"))
        log(menu(menuStr02))
        Console.WriteLine()
        printMenuLine(bmenu(anyKeyStr))
        Console.ReadKey()
    End Sub

    ''' <summary>
    ''' Compares two winapp2.ini format iniFiles and builds the output for the user containing the differences
    ''' </summary>
    ''' <returns></returns>
    Private Function compareTo() As List(Of String)
        Dim outList, comparedList As New List(Of String)
        For Each section In oldOrLocalFile.sections.Values
            ' If we're looking at an entry in the old file and the new file contains it, and we haven't yet processed this entry
            If newOrRemoteFile.sections.Keys.Contains(section.name) And Not comparedList.Contains(section.name) Then
                Dim sSection As iniSection = newOrRemoteFile.sections(section.name)
                ' And if that entry in the new file does not compareTo the entry in the old file, we have a modified entry
                Dim addedKeys, removedKeys As New keyList
                Dim updatedKeys As New List(Of KeyValuePair(Of iniKey, iniKey))
                If Not section.compareTo(sSection, removedKeys, addedKeys) Then
                    chkLsts(removedKeys, addedKeys, updatedKeys)
                    ' Silently ignore any entries with only alphabetization changes
                    If removedKeys.keyCount + addedKeys.keyCount + updatedKeys.Count = 0 Then Continue For
                    Dim tmp = getDiff(sSection, "modified")
                    tmp = getChangesFromList(addedKeys, tmp, $"{prependNewLines()}Added:")
                    tmp = getChangesFromList(removedKeys, tmp, $"{prependNewLines(addedKeys.keyCount > 0)}Removed:")
                    If updatedKeys.Count > 0 Then
                        tmp += appendNewLine($"{prependNewLines(removedKeys.keyCount > 0 Or addedKeys.keyCount > 0)}Modified:")
                        updatedKeys.ForEach(Sub(pair) appendStrs({appendNewLine(prependNewLines() & pair.Key.Name), $"Old:   {appendNewLine(pair.Key.toString)}", $"New:   {appendNewLine(pair.Value.toString)}"}, tmp))
                    End If
                    tmp += prependNewLines(False) & menuStr00
                    outList.Add(tmp)
                End If
            ElseIf Not newOrRemoteFile.sections.Keys.Contains(section.name) And Not comparedList.Contains(section.name) Then
                ' If we do not have the entry in the new file, it has been removed between versions 
                outList.Add(getDiff(section, "removed"))
            End If
            comparedList.Add(section.name)
        Next
        ' Any sections from the new file which are not found in the old file have been added
        For Each section In newOrRemoteFile.sections.Values
            If Not oldOrLocalFile.sections.Keys.Contains(section.name) Then outList.Add(getDiff(section, "added"))
        Next
        Return outList
    End Function

    ''' <summary>
    ''' Handles the Added and Removed cases for changes 
    ''' </summary>
    ''' <param name="keyList">A list of iniKeys that have been added/removed</param>
    ''' <param name="out">The output text to be appended to</param>
    ''' <param name="changeTxt">The text to appear in the output</param>
    Private Function getChangesFromList(keyList As keyList, out As String, changeTxt As String) As String
        If keyList.keyCount = 0 Then Return out
        out += appendNewLine(changeTxt)
        keyList.keys.ForEach(Sub(key) out += key.toString & Environment.NewLine)
        Return out
    End Function

    ''' <summary>
    ''' Observes lists of added and removed keys from a section for diffing, adds any changes to the updated key 
    ''' </summary>
    ''' <param name="removedKeys">The list of iniKeys that were removed from the newer version of the file</param>
    ''' <param name="addedKeys">The list of iniKeys that were added to the newer version of the file</param>
    ''' <param name="updatedKeys">The list containing iniKeys rationalized by this function as having been updated rather than added or removed</param>
    Private Sub chkLsts(ByRef removedKeys As keyList, ByRef addedKeys As keyList, ByRef updatedKeys As List(Of KeyValuePair(Of iniKey, iniKey)))
        ' Create copies of the given keylists so we can modify them during the iteration 
        Dim akTemp As New keyList(addedKeys.keys)
        Dim rkTemp As New keyList(removedKeys.keys)
        For i As Integer = 0 To addedKeys.keyCount - 1
            Dim key = addedKeys.keys(i)
            For j = 0 To removedKeys.keyCount - 1
                Dim skey = removedKeys.keys(j)
                If key.compareNames(skey) Then
                    Select Case key.KeyType
                        Case "FileKey", "ExcludeKey", "RegKey"
                            Dim oldKey As New winapp2KeyParameters(key)
                            Dim newKey As New winapp2KeyParameters(skey)
                            ' If the path has changed, the key has been updated
                            If Not oldKey.pathString = newKey.pathString Then
                                updateKeys(updatedKeys, akTemp, rkTemp, key, skey)
                                Exit For
                            End If
                            oldKey.argsList.Sort()
                            newKey.argsList.Sort()
                            ' Check the number of arguments provided to the key
                            If oldKey.argsList.Count = newKey.argsList.Count Then
                                For k = 0 To oldKey.argsList.Count - 1
                                    ' If the args count matches but the sorted state of the args doesn't, the key has been updated
                                    If Not oldKey.argsList(k).Equals(newKey.argsList(k), StringComparison.InvariantCultureIgnoreCase) Then
                                        updateKeys(updatedKeys, akTemp, rkTemp, key, skey)
                                        Exit For
                                    End If
                                Next
                                ' If we get this far, it's just an alphabetization change and can be ignored silently
                                akTemp.remove(skey)
                                rkTemp.remove(key)
                            Else
                                ' If the count doesn't match, something has definitely changed
                                updateKeys(updatedKeys, akTemp, rkTemp, key, skey)
                                Exit For
                            End If
                        Case Else
                            ' Other keys don't require such complex legwork, thankfully. If their values don't match, they've been updated
                            If Not key.compareValues(skey) Then updateKeys(updatedKeys, akTemp, rkTemp, key, skey)
                    End Select
                End If
            Next
        Next
        ' Update the lists
        addedKeys = akTemp
        removedKeys = rkTemp
    End Sub

    ''' <summary>
    ''' Performs change tracking for chkLst 
    ''' </summary>
    ''' <param name="updLst">The list of updated keys</param>
    ''' <param name="aKeys">The list of added keys</param>
    ''' <param name="rKeys">The list of removed keys</param>
    ''' <param name="key">An added key</param>
    ''' <param name="skey">A removed key</param>
    Private Sub updateKeys(ByRef updLst As List(Of KeyValuePair(Of iniKey, iniKey)), ByRef aKeys As keyList, ByRef rKeys As keyList, key As iniKey, skey As iniKey)
        updLst.Add(New KeyValuePair(Of iniKey, iniKey)(key, skey))
        rKeys.remove(skey)
        aKeys.remove(key)
    End Sub

    ''' <summary>
    ''' Returns a string containing a menu box listing the change type and entry, followed by the entry's toString
    ''' </summary>
    ''' <param name="section">an iniSection object to be diffed</param>
    ''' <param name="changeType">The type of change to observe</param>
    ''' <returns></returns>
    Private Function getDiff(section As iniSection, changeType As String) As String
        Dim out  = ""
        appendStrs({appendNewLine(mkMenuLine($"{section.name} has been {changeType}.", "c")), appendNewLine(appendNewLine(mkMenuLine(menuStr02, ""))), appendNewLine(section.ToString)}, out)
        If Not changeType = "modified" Then out += menuStr00
        Return out
    End Function

    ''' <summary>
    ''' Saves a String to the log file
    ''' </summary>
    ''' <param name="toLog">The string to be appended to the log</param>
    Private Sub log(toLog As String)
        cwl(toLog)
        outputToFile += appendNewLine(toLog)
    End Sub
End Module