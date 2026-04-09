FROM python:latest
ARG BRSCAN_DEB=brscan4-0.4.9-1.amd64.deb
MAINTAINER Esben Haabendal, esben@haabendal.dk

# This is where the scan output will be written to
VOLUME /output/scan

# This must be mapped to ${ADVERTISE_IP}:54925
EXPOSE 54925/udp

# Install required Debian packages
RUN apt-get update -q \
 && apt-get install -q -y sane-utils poppler-utils libusb-0.1-4 pdftk \
 && apt-get clean \
 && rm -rf /var/lib/apt/lists/*

# Install required python modules
COPY requirements.txt /tmp/
RUN pip install -r /tmp/requirements.txt

# Install Brother scanner driver
COPY $BRSCAN_DEB /tmp/
RUN dpkg --install /tmp/brscan4-*.deb

# Fixup symbolic links
RUN mkdir /usr/lib/sane \
 && for f in /usr/lib64/sane/libsane-brother*;do ln -s $f /usr/lib/sane/;done

# Install brscan (run "setup.py build sdist" before docker build)
COPY . /src
RUN cd /src && python3 setup.py build sdist
#COPY dist/brscan-0.0.1.tar.gz /tmp/
RUN pip install --no-binary :all: /src/dist/brscan-*.tar.gz

# Disable ImageMagick policies for now
RUN rm /etc/ImageMagick-6/policy.xml

# Add run script and set it as default command
ADD run.sh /
CMD /run.sh
