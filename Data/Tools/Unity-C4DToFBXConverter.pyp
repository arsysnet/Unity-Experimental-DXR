"""
Unity C4D To FBX Converter
Written for Cinema 4D R18.020

"""
import platform
import c4d
import sys
import os
from c4d import documents, plugins, threading, utils, gui

# Be sure to use a unique ID obtained from www.plugincafe.com
PLUGIN_ID = 1038605
FBX_EXPORTER_ID = 1026370

FBXEXPORT_TEXTURES = True
FBXEXPORT_EMBED_TEXTURES = False
FBXEXPORT_SAVE_NORMALS = True
FBXEXPORT_ASCII = False

def win32_utf8_argv():                                                                                               
    """
    Adapted from http://code.activestate.com/recipes/572200-get-sysargv-with-unicode-characters-under-windows/

    Uses shell32.GetCommandLineArgvW to get sys.argv as a list of UTF-8
    strings.

    Versions 2.5 and older of Python don't support Unicode in sys.argv on
    Windows, with the underlying Windows API instead replacing multi-byte
    characters with '?'.
    """

    from ctypes import POINTER, byref, cdll, c_int, windll
    from ctypes.wintypes import LPCWSTR, LPWSTR

    GetCommandLineW = cdll.kernel32.GetCommandLineW
    GetCommandLineW.argtypes = []
    GetCommandLineW.restype = LPCWSTR

    CommandLineToArgvW = windll.shell32.CommandLineToArgvW
    CommandLineToArgvW.argtypes = [LPCWSTR, POINTER(c_int)]
    CommandLineToArgvW.restype = POINTER(LPWSTR)

    cmd = GetCommandLineW()
    argc = c_int(0)
    argv = CommandLineToArgvW(cmd, byref(argc))
    if argc.value > 0:
        # Remove Python executable if present
        if argc.value - len(sys.argv) == 1:
            start = 1
        else:
            start = 0
        return [argv[i].encode('utf-8') for i in
                xrange(start, argc.value)]
    else:
        return []

class ExportThread(threading.C4DThread):

    def __init__(self, doc, exportPath):
        self.doc = doc
        self.status = False
        self.exportPath = exportPath

    def Main(self):
        # Export document to FBX
        self.status = documents.SaveDocument(self.doc, self.exportPath, c4d.SAVEDOCUMENTFLAGS_DONTADDTORECENTLIST, FBX_EXPORTER_ID)

    def GetStatus(self):
        return self.status

class MessageLogger():

    def __init__(self, logOutputPath=None):
        self.logString = ""
        if logOutputPath :
            self.logOutputFile = open(logOutputPath, 'w')
        else :
            self.logOutputFile = sys.stdout

    def appendMessage(self,message) :
        self.logString+= message + "\n"

    def writeLog(self, success) :
        if success :
            self.logOutputFile.write("SUCCESS\nframerate=" + str(documents.GetActiveDocument().GetFps()) + "\n")
        else :
            self.logOutputFile.write("FAILURE\n" + self.logString)

        if self.logOutputFile is not sys.stdout :
            self.logOutputFile.close()
        return success

# get command line arguments
def PluginMessage(id, data) :
    if id==c4d.C4DPL_COMMANDLINEARGS:
        if platform.system() == 'Windows':
            argv = win32_utf8_argv()
        else:
            argv = sys.argv

        if '-UnityC4DtoFBX' in argv :
            sourcePath = ""
            destinationPath = ""
            logOutputPath = ""
            textureSearchPath = ""
            # parse command line arguments
            for idx, arg in enumerate(argv):
                if (arg == "-src" and len(argv) > idx) :
                    sourcePath = argv[idx+1]
                if (arg == "-dst" and len(argv) > idx) :
                    destinationPath = argv[idx+1]
                if (arg == "-out" and len(argv) > idx) :
                    logOutputPath = argv[idx+1]
                if (arg == "-textureSearchPath" and len(argv) > idx) :
                    textureSearchPath = argv[idx+1]

            logger = MessageLogger(logOutputPath)
            
            if not sourcePath or not destinationPath :
                logger.appendMessage("Invalid command line arguments")
                return logger.writeLog(False)
            
            # C4D automatically opens any file which path is passed as an argument, 
            # to prevent that, the parameters omit file extensions.
            sourcePath+=".c4d"
            destinationPath+=".fbx"

            # load the c4d scene
            loadedDoc = documents.LoadFile(sourcePath)
            documents.GetActiveDocument().SetDocumentPath(textureSearchPath)
            if loadedDoc :    
                logger.appendMessage("Succesfuly loaded " + sourcePath)
            else :
                logger.appendMessage("Couldn't load " + sourcePath)
                return logger.writeLog(False)
            # execute the conversion
            if ExecuteConversion(destinationPath, textureSearchPath, logger):
                logger.appendMessage("FBX Exported at " + destinationPath)
                return logger.writeLog(True)
            else :
                logger.appendMessage("FBX Plugin failed to export\n")
                return logger.writeLog(False)
        else :
            # command line argument not found. Do nothing.
            return False

def ExecuteConversion(destinationPath, textureSearchPath, logger) :
    plug = plugins.FindPlugin(FBX_EXPORTER_ID, c4d.PLUGINTYPE_SCENESAVER)
    if plug is None:
        logger.appendMessage("FBX Export plugin not found")
        return False

    op = {}
    # Retrieve FBX Eporter settings object
    if plug.Message(c4d.MSG_RETRIEVEPRIVATEDATA, op) :
        if "imexporter" not in op:
            return False

        # BaseList2D object stored in "imexporter" key hold the settings
        fbxExport = op["imexporter"]
        if fbxExport is None :
            return False

        # Change export settings
        
        fbxExport[c4d.FBXEXPORT_TEXTURES] = FBXEXPORT_TEXTURES
        fbxExport[c4d.FBXEXPORT_EMBED_TEXTURES] = FBXEXPORT_EMBED_TEXTURES
        fbxExport[c4d.FBXEXPORT_SAVE_NORMALS] = FBXEXPORT_SAVE_NORMALS
        fbxExport[c4d.FBXEXPORT_ASCII] = FBXEXPORT_ASCII

        c4d.SetGlobalTexturePath(9, documents.GetActiveDocument().GetDocumentPath())
        if textureSearchPath :
            c4d.SetGlobalTexturePath(8, textureSearchPath)

        thread = ExportThread(documents.GetActiveDocument(), destinationPath)
        thread.Start()       # Start thread
        thread.End()         # Then end it but wait until it finishes

        # Retrieve export status and return it
        status = thread.GetStatus()
        return status
    else :
        logger.appendMessage("Could not configure FBX plugin")
        return False

    documents.KillDocument(documents.GetActiveDocument())
