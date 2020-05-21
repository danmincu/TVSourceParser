killall vlc
screen -S acestream -d -m acestreamengine --client-console
screen -S aceproxy -d -m python aceproxy/acehttp.py
rm /home/user/Desktop/playlist.xspf
rm playlist.xspf
wget https://raw.githubusercontent.com/danmincu/TVSourceParser/master/Sample/playlist.xspf
vlc /home/user/Desktop/playlist.xspf