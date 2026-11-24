#version 330 core
out vec4 FragColor;

uniform vec3 uLightColor;
uniform vec3 uLightPos;
uniform vec3 uViewPos;

uniform float uShininess;

		
in vec4 outCol;
in vec3 outNormal;
in vec3 outWorldPosition;

void main()
{
    float ambientStrength = 0.1;
    vec3 ambient = ambientStrength * uLightColor;

    float diffuseStrength = 0.3;
    vec3 norm = normalize(outNormal);
    vec3 lightDir = normalize(uLightPos - outWorldPosition);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * uLightColor * diffuseStrength;

    float specularStrength = 0.6;
    vec3 viewDir = normalize(uViewPos - outWorldPosition);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), uShininess);

    vec3 result = (ambient + diffuse + spec) * outCol.rgb;

    FragColor = vec4(result, outCol.w);
}