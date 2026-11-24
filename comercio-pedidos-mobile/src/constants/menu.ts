import type { ImageSource } from 'expo-image';
export type MenuProduct = {id: number; nombre: string; descripcion: string; precio: number; categoria: string; emoji: string; imagen?: ImageSource | number};
export const negocio = { nombre: 'Burger Club', whatsapp: process.env.EXPO_PUBLIC_WHATSAPP?.trim() || '5491124830571', envio: 1500 };
export const menu: MenuProduct[] = [
 { id: 1, nombre: 'La Clásica', descripcion: 'Carne smash, cheddar, lechuga, tomate y salsa de la casa.', precio: 8500, categoria: 'Burgers', emoji: '🍔', imagen: require('../../assets/images/menu/classic.jpg') },
 { id: 2, nombre: 'Doble Bacon', descripcion: 'Doble carne, doble cheddar, bacon crocante y barbacoa.', precio: 11500, categoria: 'Burgers', emoji: '🥓', imagen: require('../../assets/images/menu/bacon.jpg') },
 { id: 3, nombre: 'Crispy Onion', descripcion: 'Carne smash, cheddar, cebolla crispy y alioli.', precio: 9800, categoria: 'Burgers', emoji: '🍔', imagen: require('../../assets/images/menu/onion.jpg') },
 { id: 4, nombre: 'Veggie Club', descripcion: 'Medallón de garbanzos, rúcula, tomate y mayonesa vegetal.', precio: 8900, categoria: 'Burgers', emoji: '🌱' },
 { id: 5, nombre: 'Papas clásicas', descripcion: 'Doradas, crocantes y con el punto justo de sal.', precio: 3500, categoria: 'Papas', emoji: '🍟', imagen: require('../../assets/images/menu/fries-classic.jpg') },
 { id: 6, nombre: 'Papas cheddar', descripcion: 'Papas con cheddar fundido y bacon.', precio: 5200, categoria: 'Papas', emoji: '🍟', imagen: require('../../assets/images/menu/fries-cheddar.jpg') },
 { id: 7, nombre: 'Coca-Cola · lata 354 ml', descripcion: 'Sabor original. Bien fría para acompañar tu burger.', precio: 2500, categoria: 'Bebidas', emoji: '🥤', imagen: require('../../assets/images/menu/coca-can.jpg') },
 { id: 8, nombre: 'Coca-Cola Sin Azúcar · lata 354 ml', descripcion: 'Todo el sabor, sin azúcar.', precio: 2500, categoria: 'Bebidas', emoji: '🥤', imagen: require('../../assets/images/menu/coca-zero-can.jpg') },
 { id: 9, nombre: 'Sprite · lata 354 ml', descripcion: 'Lima-limón, fresca y burbujeante.', precio: 2500, categoria: 'Bebidas', emoji: '🥤', imagen: require('../../assets/images/menu/sprite-can.jpg') },
 { id: 10, nombre: 'Fanta Naranja · lata 354 ml', descripcion: 'Un toque de naranja para tu pedido.', precio: 2500, categoria: 'Bebidas', emoji: '🥤', imagen: require('../../assets/images/menu/fanta-can.jpg') },
 { id: 11, nombre: 'Coca-Cola · botella 1,25 L', descripcion: 'Sabor original, tamaño para compartir.', precio: 4500, categoria: 'Bebidas', emoji: '🥤', imagen: require('../../assets/images/menu/coca-bottle.jpg') },
 { id: 12, nombre: 'Sprite · botella 1,25 L', descripcion: 'Lima-limón en botella, ideal para compartir.', precio: 4500, categoria: 'Bebidas', emoji: '🥤', imagen: require('../../assets/images/menu/sprite-bottle.jpg') },
];
export const moneda = (valor: number) => '$ ' + valor.toLocaleString('es-AR');

