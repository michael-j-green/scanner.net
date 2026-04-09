import subprocess
import os
import datetime
import wand.image
import glob
import time

def pnmtopdf(pnmfile, pdffile, resolution=None):
    with wand.image.Image(filename=pnmfile, resolution=resolution) as pnm:
        with pnm.convert('pdf') as pdf:
            pdf.save(filename=pdffile)
    os.remove(pnmfile)

scan_options = {
    'device': '--device-name',
    'resolution': '--resolution',
    'mode': '--mode',
    'source': '--source',
    'brightness': '--brightness',
    'contrast': '--contrast',
    'width': '-x',
    'height': '-y',
    'left': '-l',
    'top': '-t',
}

def add_scan_options(cmd, options):
    for name, arg in scan_options.items():
        if name in options:
            cmd += [arg, str(options[name])]
    cmd = [ str(c) for c in cmd ]

def scanto(func, options):
    print('scanto %s %s'%(func, options))
    options = options.copy()
    #if func == 'FILE':
    if not 'dir' in options:
        options['dir'] = '/tmp'
    dst = options['dir']
    os.makedirs(dst, exist_ok=True)

    tmp = '/tmp/scan'
    os.makedirs(tmp, exist_ok=True)

    now = datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
    adf = options.pop('adf', False)

    time.sleep(30)

    if adf:
        cmd = ['scanadf',
               '--output-file', os.path.join(tmp, 'scan_%s_%%d.pnm'%(now))]
        add_scan_options(cmd, options)
        print('# ' + ' '.join(cmd))
        subprocess.call(cmd)
        pnmfiles = []
        pdffiles = []
        for pnmfile in glob.glob(os.path.join(tmp, 'scan_%s_*.pnm'%(now))):
            pdffile = '%s.pdf'%(pnmfile[:-4])
            pnmtopdf(pnmfile, pdffile, options['resolution'])
            pnmfiles.append(pnmfile)
            pdffiles.append(pdffile)
        if dst != '/output/duplex':
            cmd = ['pdfunite'] + pdffiles + [os.path.join(dst, 'scan_%s.pdf'%(now))]
        else:
            if os.path.exists(os.path.join(dst, 'odd.pdf')):
                cmd = ['pdfunite'] + pdffiles + [os.path.join(dst, 'even.pdf')]
            else:
                cmd = ['pdfunite'] + pdffiles + [os.path.join(dst, 'odd.pdf')]

        print('# ' + ' '.join(cmd))
        subprocess.call(cmd)
        time.sleep(3)
        if dst == '/output/duplex':
            if os.path.exists(os.path.join(dst, 'even.pdf')):
                cmdS = 'pdftk A=' + os.path.join(dst, 'odd.pdf') + ' B=' + os.path.join(dst, 'even.pdf') + ' shuffle A Bend-1 output ' + os.path.join(dst, 'scan_%s.pdf'%(now))
                subprocess.call(cmdS, shell=True)
                os.remove(os.path.join(dst, 'even.pdf'))
                os.remove(os.path.join(dst, 'odd.pdf'))
        for f in pdffiles:
            os.remove(f)
    else:
        cmd = ['scanimage']
        add_scan_options(cmd, options)
        pnmfile = os.path.join(dst, 'scan_%s.pnm'%(now))
        with open(pnmfile, 'w') as pnm:
            print('# ' + ' '.join(cmd))
            process = subprocess.Popen(cmd, stdout=pnm)
            process.wait()
        pdffile = '%s.pdf'%(pnmfile[:-4])
        pnmtopdf(pnmfile, pdffile, options['resolution'])
        print('Wrote', pdffile)
