FROM --platform=$BUILDPLATFORM ubuntu
WORKDIR /app

ARG TARGETPLATFORM
ARG BUILDPLATFORM
ARG TARGETOS
ARG TARGETARCH
ARG ZIG_TARGETOS
ARG ZIG_TARGETARCH

RUN export BUILDKIT_PROGRESS=plain

RUN echo "BUILDPLATFORM=$BUILDPLATFORM"
RUN echo "TARGETPLATFORM=$TARGETPLATFORM"
RUN echo "TARGETOS=$TARGETOS"
RUN echo "TARGETARCH=$TARGETARCH"
RUN echo "ZIG_TARGETOS=$ZIG_TARGETOS"
RUN echo "ZIG_TARGETARCH=$ZIG_TARGETARCH"

RUN apt-get update
RUN apt-get install -y file --fix-missing
RUN mkdir -p /app/libs

# Libsodium

#RUN apt-get install -y git curl jq xz-utils --fix-missing
#
#COPY zig.sh /app/zig.sh
#RUN chmod +x zig.sh
#RUN ./zig.sh ${ZIG_TARGETOS} ${ZIG_TARGETARCH}
#RUN mkdir -p /usr/bin/zig
#RUN mv zig/zig /usr/bin/zig/zig
#RUN chmod +rwx /usr/bin/zig/zig
#RUN chown -R root:root /usr/bin/zig/zig
#RUN mv zig/lib /usr/lib/zig
#RUN chown -R root:root /usr/lib/zig
#RUN cd /app
#
#RUN git clone --depth 1 https://github.com/jedisct1/libsodium.git
#RUN cd libsodium && zig build -Doptimize=ReleaseFast -Dtarget=${ZIG_TARGETOS}-${ZIG_TARGETARCH}
#RUN cd /app
#
#RUN cp ./zig-out/bin/libsodium.so /app/libs/libsodium.so

# Opus

RUN apt-get install -y wget git autoconf automake libtool gcc make coreutils p7zip-full p7zip-rar --fix-missing

RUN git clone --depth 1 https://gitlab.xiph.org/xiph/opus.git
RUN cd /app/opus && ./autogen.sh && ./configure && make && make install
RUN cp $(readlink -f /app/opus/.libs/libopus.so) /app/libs/libopus.so
RUN file /app/libs/libopus.so > /app/libs/libopus.so.info
RUN 7z a -tzip /app/libs/libopus.so.${TARGETOS}-${TARGETARCH}.zip /app/libs/libopus.so /app/libs/libopus.so.info

ENTRYPOINT ["bash", "-c", "while true; do sleep 1; done"]