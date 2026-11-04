// Descarga en el navegador sin pasar por el servidor: Blob + enlace temporal. La URL se revoca
// con un retardo porque Safari y Firefox pueden cancelar la descarga si se revoca al instante.
export function descargarArchivo(nombre: string, partes: BlobPart[], tipo: string) {
  const url = URL.createObjectURL(new Blob(partes, { type: tipo }))
  const enlace = document.createElement('a')
  enlace.href = url
  enlace.download = nombre
  document.body.append(enlace)
  enlace.click()
  enlace.remove()
  window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
