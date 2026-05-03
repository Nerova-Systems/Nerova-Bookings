import { Img } from "@react-email/components";

type ImageProps = {
  src: string;
  alt: string;
  width?: string | number;
  height?: string | number;
  className?: string;
};

export function Image({ src, alt, width, height, className }: ImageProps) {
  return <Img src={src} alt={alt} width={width} height={height} className={className} />;
}
