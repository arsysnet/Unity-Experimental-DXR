#!/usr/bin/env python

################################################################################
#
# convert.py
#
# Version: 1.000
#
# Author: Gwynne Reddick
#
# Description:
#
#
# Usage: @convert.py {infile} {outfile} {format} {logfile}
#
# Arguments: infile     -   path and filename of input file
#            outfile    -   path and filename of output file
#            format     -   output file format. for FBX this is either 'FBX' or ' FBX 2006.11'
#            logfile    -   path and filename of output lig (optional)
#
# Examples: @convert.py {L:\Luxology\fbxtest.lxo} {L:\Luxology\FBXout.fbx} {FBX} {L:\Luxology\logout.txt}
#           @convert.py {L:\Luxology\fbxtest.lxo} {L:\Luxology\FBXout.fbx} {FBX 2006.11} {}
#
#
# Last Update 12:03 09/04/10
#
################################################################################

try:
    lx.eval('log.toConsole true')
    argstring = lx.arg()
    args = argstring.split('} ')
    for index, arg in enumerate(args):
        args[index] = arg.strip('{}')
    try:
        infile, outfile, logfile = args
    except:
        raise Exception('Wrong number of arguments provided')
    format = 'FBX'
    extension = 'fbx'
    # set useCollada to 0 if you want to use FBX as intermediate format
    useCollada = 1
    if useCollada == 1:
        format = 'COLLADA_141'
        extension = 'dae'
    try:
        lx.eval('scene.open {%s} normal' % infile)
        lx.eval('!!scene.saveAs {%s} {%s} false' % (outfile + '.' + extension, format))
    except:
        lx.out('Exception "%s" on line: %d' % (sys.exc_value, sys.exc_traceback.tb_lineno))
    if logfile:
        lx.eval('log.masterSave {%s}' % logfile)
except:
    lx.out('Exception "%s" on line: %d' % (sys.exc_value, sys.exc_traceback.tb_lineno))
lx.eval('app.quit')
