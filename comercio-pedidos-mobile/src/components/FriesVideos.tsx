import { useEvent } from 'expo';
import { Image } from 'expo-image';
import { useVideoPlayer, VideoView } from 'expo-video';
import { useState } from 'react';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
const clips = [
 {title: 'Doradas y crocantes', poster: require('../../assets/images/menu/fries.jpg'), video: require('../../assets/images/menu/fries.mp4')},
 {title: 'El punto justo', poster: require('../../assets/images/menu/fries-cooking.jpg'), video: require('../../assets/images/menu/fries-cooking.mp4')},
];
function Player({clip, close}: {clip: typeof clips[number]; close: () => void}) {
 const player = useVideoPlayer(clip.video, p => {p.muted = true; p.loop = true; p.play();});
 const {isPlaying} = useEvent(player, 'playingChange', {isPlaying: player.playing});
 const {status} = useEvent(player, 'statusChange', {status: player.status});
 return <SafeAreaView style={s.player}><Pressable accessibilityRole="button" onPress={close} style={s.close}><Text style={s.title}>Cerrar video ×</Text></Pressable><Text style={s.title}>{clip.title}</Text><Pressable accessibilityRole="button" style={s.close} onPress={() => isPlaying ? player.pause() : player.play()}><Text style={s.title}>{isPlaying ? 'Pausar' : 'Reproducir'}</Text></Pressable><VideoView player={player} style={s.video} nativeControls contentFit="contain" />{status === 'error' && <Text style={s.title}>No se pudo reproducir el video. Cerralo e intentá nuevamente.</Text>}</SafeAreaView>;
}
export default function FriesVideos() {
 const [selected, setSelected] = useState<number | null>(null);
 return <View style={{gap: 14}}><Text style={s.heading}>El crunch también se ve.</Text><Text style={s.caption}>Dale play a tus próximas papas favoritas.</Text><View style={s.row}>{clips.map((c,i) => <Pressable key={c.title} accessibilityRole="button" accessibilityLabel={`Ver video: ${c.title}`} onPress={() => setSelected(i)} style={s.card}><Image source={c.poster} contentFit="cover" style={s.poster}/><View style={s.play}><Text style={s.playText}>▶</Text></View><Text style={s.title}>{c.title}</Text></Pressable>)}</View><Modal visible={selected !== null} animationType="slide" onRequestClose={() => setSelected(null)}>{selected !== null && <Player clip={clips[selected]} close={() => setSelected(null)}/>}</Modal></View>;
}
const s = StyleSheet.create({heading:{color:'#f6f2eb',fontSize:22,fontWeight:'800'},caption:{color:'#adafa4',fontSize:13},row:{flexDirection:'row',gap:12},card:{flex:1,borderRadius:18,overflow:'hidden',backgroundColor:'#25281f',paddingBottom:14},poster:{width:'100%',aspectRatio:1.2},play:{position:'absolute',top:18,left:14,borderRadius:24,backgroundColor:'#ff8a3d',width:42,height:42,alignItems:'center',justifyContent:'center'},playText:{color:'#171710',fontSize:18},title:{color:'#f6f2eb',fontWeight:'700',fontSize:14,padding:12},player:{flex:1,backgroundColor:'#101110',gap:15},close:{alignSelf:'flex-end',padding:12},video:{width:'100%',flex:1}});
